using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace UnpackerGenerator
{
    [Generator]
    public class IEnumerableUnpackerSourceGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var unpackableClasses = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => s is ClassDeclarationSyntax cds && cds.HasAttribute("Unpackable"),
                    transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
                .Where(static m => m is not null);

            context.RegisterSourceOutput(unpackableClasses, (spc, classDeclaration) =>
            {
                var className = classDeclaration.Identifier.Text;
                var namespaceName = classDeclaration.FirstAncestorOrSelf<NamespaceDeclarationSyntax>()?.Name.ToString() ?? "IEnumerableUnpacker";

                var sourceCode = GenerateExtensionsClass(classDeclaration);

                spc.AddSource($"{className}Extensions.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
            });
        }
        private static string GetGenericTypeParameterString(string[] genericTypeParameters)
        {
            return genericTypeParameters.Length > 0 ? $"<{string.Join(", ", genericTypeParameters)}>" : string.Empty;
        }

        private static string GenerateExtensionsClass(ClassDeclarationSyntax classDeclaration)
        {
            var sb = new StringBuilder();

            var className = classDeclaration.Identifier.Text;
            var namespaceName = classDeclaration.FirstAncestorOrSelf<NamespaceDeclarationSyntax>()?.Name.ToString() ?? "IEnumerableUnpacker";

            var fields = classDeclaration.Members.OfType<FieldDeclarationSyntax>();
            var genericTypeParameters = classDeclaration.TypeParameterList?.Parameters.Select(p => p.Identifier.Text).ToArray() ?? new string[0];
            var genericTypeParameterString = GetGenericTypeParameterString(genericTypeParameters);

            GenerateUsings(sb);
            GenerateNamespaceAndClassStart(sb, namespaceName, className);
            GenerateParallelStateStruct(sb, className, genericTypeParameterString, fields);
            GenerateProcessMethod(sb, className, genericTypeParameterString, fields);
            GenerateMethodSignature(sb, className, genericTypeParameterString, fields);
            GenerateMethodBody(sb, className, genericTypeParameterString, fields);
            GenerateClassEnd(sb);

            return sb.ToString();
        }

        private static void GenerateUsings(StringBuilder sb)
        {
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine();
        }

        private static void GenerateParallelStateStruct(StringBuilder sb, string className, string genericTypeParameterString, IEnumerable<FieldDeclarationSyntax> fields)
        {
            sb.AppendLine($"        private unsafe struct ParallelState{genericTypeParameterString}");
            sb.AppendLine("        {");
            sb.AppendLine($"            public {className}{genericTypeParameterString}[] {className}Array;");

            foreach (var field in fields)
            {
                var (elementType, isIEnumerable, isArray, unpackAttributeArgument) = GetFieldInfo(field);
                if (unpackAttributeArgument != null)
                {
                    unpackAttributeArgument = unpackAttributeArgument.Trim('"');
                    if (IsBlittableType(elementType))
                    {
                        sb.AppendLine($"            public {elementType}* P{unpackAttributeArgument};");
                    }
                    else
                    {
                        sb.AppendLine($"            public {elementType}{(isIEnumerable || isArray ? "[,]" : "[]")} {unpackAttributeArgument};");
                    }
                }
            }

            foreach (var field in fields)
            {
                var (_, isIEnumerable, isArray, unpackAttributeArgument) = GetFieldInfo(field);
                if (unpackAttributeArgument != null && (isIEnumerable || isArray))
                {
                    unpackAttributeArgument = unpackAttributeArgument.Trim('"');
                    sb.AppendLine($"            public int {unpackAttributeArgument}Length;");
                }
            }

            sb.AppendLine("        }");
        }

        private static void GenerateProcessMethod(StringBuilder sb, string className, string genericTypeParameterString, IEnumerable<FieldDeclarationSyntax> fields)
        {
            sb.AppendLine($"        private static unsafe void Process{className}{genericTypeParameterString}(ParallelState{genericTypeParameterString} state, int i)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var item = state.{className}Array[i];");

            foreach (var field in fields)
            {
                var (elementType, isIEnumerable, isArray, unpackAttributeArgument) = GetFieldInfo(field);
                if (unpackAttributeArgument != null)
                {
                    unpackAttributeArgument = unpackAttributeArgument.Trim('"');

                    if (!isIEnumerable && !isArray)
                    {
                        if (IsBlittableType(elementType))
                        {
                            sb.AppendLine($"            state.P{unpackAttributeArgument}[i] = item.{field.Declaration.Variables[0].Identifier};");
                        }
                        else
                        {
                            sb.AppendLine($"            state.{unpackAttributeArgument}[i] = item.{field.Declaration.Variables[0].Identifier};");
                        }
                    }
                }
            }

            sb.AppendLine();

            foreach (var field in fields)
            {
                var (elementType, isIEnumerable, isArray, unpackAttributeArgument) = GetFieldInfo(field);
                if (unpackAttributeArgument != null && isIEnumerable || isArray)
                {
                    unpackAttributeArgument = unpackAttributeArgument.Trim('"');

                    if (IsBlittableType(elementType))
                    {
                        sb.AppendLine($"            {elementType}* p{unpackAttributeArgument}Dest = state.P{unpackAttributeArgument} + i * state.{unpackAttributeArgument}Length;");
                    }
                }
            }

            sb.AppendLine();

            foreach (var field in fields)
            {
                var (elementType, isIEnumerable, isArray, unpackAttributeArgument) = GetFieldInfo(field);
                if (unpackAttributeArgument != null && isIEnumerable || isArray)
                {
                    unpackAttributeArgument = unpackAttributeArgument.Trim('"');

                    if (IsBlittableType(elementType))
                    {
                        sb.AppendLine($"            fixed ({elementType}* p{unpackAttributeArgument}Src = item.{field.Declaration.Variables[0].Identifier})");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                int {unpackAttributeArgument}ByteLength = state.{unpackAttributeArgument}Length * sizeof({elementType});");
                        sb.AppendLine($"                System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(p{unpackAttributeArgument}Dest, p{unpackAttributeArgument}Src, (uint){unpackAttributeArgument}ByteLength);");
                        sb.AppendLine("            }");
                    }
                    else
                    {
                        sb.AppendLine($"            for (int j = 0; j < state.{unpackAttributeArgument}Length; j++)");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                state.{unpackAttributeArgument}[i, j] = item.{field.Declaration.Variables[0].Identifier}[j];");
                        sb.AppendLine("            }");
                    }
                }
            }

            sb.AppendLine("        }");
        }
        private static void GenerateNamespaceAndClassStart(StringBuilder sb, string namespaceName, string className)
        {
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
            sb.AppendLine($"    public static class {className}Extensions");
            sb.AppendLine("    {");
        }


        private static void GenerateMethodSignature(StringBuilder sb, string className, string genericTypeParameterString, IEnumerable<FieldDeclarationSyntax> fields)
        {
            var methodName = $"Unpack{className}";
            sb.Append($"        public static unsafe void {methodName}{genericTypeParameterString}(this IEnumerable<{className}{genericTypeParameterString}> source");

            foreach (var field in fields)
            {
                var (elementType, isIEnumerable, isArray, unpackAttributeArgument) = GetFieldInfo(field);
                if (unpackAttributeArgument != null)
                {
                    unpackAttributeArgument = unpackAttributeArgument.Trim('"');
                    var paramType = $"{elementType}{(isIEnumerable || isArray ? "[,]" : "[]")}";
                    sb.Append($", out {paramType} {unpackAttributeArgument}");
                }
            }

            sb.AppendLine(")");
        }

        private static void GenerateMethodBody(StringBuilder sb, string className, string genericTypeParameterString, IEnumerable<FieldDeclarationSyntax> fields)
        {
            sb.AppendLine("        {");
            sb.AppendLine($"            var {ToLowerFirstChar(className)}Array = source.ToArray();");
            sb.AppendLine($"            int length = {ToLowerFirstChar(className)}Array.Length;");
            sb.AppendLine("            if (length == 0)");
            sb.AppendLine("            {");

            foreach (var field in fields)
            {
                var (elementType, isIEnumerable, isArray, unpackAttributeArgument) = GetFieldInfo(field);
                if (unpackAttributeArgument != null)
                {
                    unpackAttributeArgument = unpackAttributeArgument.Trim('"');
                    sb.AppendLine($"                {unpackAttributeArgument} = new {elementType}{(isIEnumerable || isArray ? "[0, 0]" : "[0]")};");
                }
            }

            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();

            foreach (var field in fields)
            {
                var (elementType, isIEnumerable, isArray, unpackAttributeArgument) = GetFieldInfo(field);
                if (unpackAttributeArgument != null && isIEnumerable || isArray)
                {
                    unpackAttributeArgument = unpackAttributeArgument.Trim('"');
                    sb.AppendLine($"            var {ToLowerFirstChar(unpackAttributeArgument)}Length = {ToLowerFirstChar(className)}Array[0].{field.Declaration.Variables[0].Identifier}.Length;");
                }
            }

            sb.AppendLine();

            foreach (var field in fields)
            {
                var (elementType, isIEnumerable, isArray, unpackAttributeArgument) = GetFieldInfo(field);
                if (unpackAttributeArgument != null)
                {
                    unpackAttributeArgument = unpackAttributeArgument.Trim('"');
                    if (isIEnumerable || isArray)
                    {
                        sb.AppendLine($"            {unpackAttributeArgument} = new {elementType}[length, {ToLowerFirstChar(unpackAttributeArgument)}Length];");
                    }
                    else
                    {
                        sb.AppendLine($"            {unpackAttributeArgument} = new {elementType}[length];");
                    }
                }
            }

            sb.AppendLine();

            var blittableFields = fields.Where(f => IsBlittableType(GetFieldInfo(f).ElementType) && GetFieldInfo(f).UnpackAttributeArgument != null);
            foreach (var field in blittableFields)
            {
                var (elementType, _, _, unpackAttributeArgument) = GetFieldInfo(field);
                unpackAttributeArgument = unpackAttributeArgument.Trim('"');
                sb.AppendLine($"            fixed ({elementType}* p{unpackAttributeArgument} = {unpackAttributeArgument})");
            }

            sb.AppendLine("            {");
            sb.AppendLine($"                var state = new ParallelState{genericTypeParameterString}");
            sb.AppendLine("                {");
            sb.AppendLine($"                    {className}Array = {ToLowerFirstChar(className)}Array,");

            foreach (var field in fields)
            {
                var (_, _, _, unpackAttributeArgument) = GetFieldInfo(field);
                if (unpackAttributeArgument != null)
                {
                    unpackAttributeArgument = unpackAttributeArgument.Trim('"');
                    if (IsBlittableType(GetFieldInfo(field).ElementType))
                    {
                        sb.AppendLine($"                    P{unpackAttributeArgument} = p{unpackAttributeArgument},");
                    }
                    else
                    {
                        sb.AppendLine($"                    {unpackAttributeArgument} = {unpackAttributeArgument},");
                    }
                }
            }

            foreach (var field in fields)
            {
                var (_, isIEnumerable, isArray, unpackAttributeArgument) = GetFieldInfo(field);
                if (unpackAttributeArgument != null && isIEnumerable || isArray)
                {
                    unpackAttributeArgument = unpackAttributeArgument.Trim('"');
                    sb.AppendLine($"                    {unpackAttributeArgument}Length = {ToLowerFirstChar(unpackAttributeArgument)}Length,");
                }
            }

            sb.AppendLine("                };");
            sb.AppendLine();
            sb.AppendLine($"                Parallel.For(0, length, (int i) => Process{className}{genericTypeParameterString}(state, i));");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
        }
        private static bool IsBlittableType(string typeName)
        {
            var blittableTypes = new[] { "int", "float", "double", "byte", "short", "long", "sbyte", "ushort", "uint", "ulong" };
            return blittableTypes.Contains(typeName);
        }

        private static string ToLowerFirstChar(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return char.ToLowerInvariant(input[0]) + input.Substring(1);
        }

        private static void GenerateClassEnd(StringBuilder sb)
        {
            sb.AppendLine("    }");
            sb.AppendLine("}");
        }

        private static (string ElementType, bool IsIEnumerable, bool IsArray, string UnpackAttributeArgument) GetFieldInfo(FieldDeclarationSyntax field)
        {
            var fieldType = field.Declaration.Type.ToString();
            var isArray = fieldType.EndsWith("[]");
            var elementType = isArray ? fieldType.Substring(0, fieldType.Length - 2) : fieldType;
            var isIEnumerable = elementType.StartsWith("IEnumerable<") || elementType.StartsWith("List<") || elementType.StartsWith("IList<") || elementType.StartsWith("ICollection<");
            var unpackAttributeArgument = field.AttributeLists
                .SelectMany(al => al.Attributes)
                .FirstOrDefault(a => a.Name.ToString() == "Unpack")?
                .ArgumentList?
                .Arguments
                .FirstOrDefault()
                ?.Expression
                ?.ToString();

            return (elementType, isIEnumerable, isArray, unpackAttributeArgument);
        }
    }

    internal static class SyntaxExtensions
    {
        public static bool HasAttribute(this ClassDeclarationSyntax classDeclaration, string attributeName)
        {
            return classDeclaration.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => a.Name.ToString() == attributeName);
        }
    }
}