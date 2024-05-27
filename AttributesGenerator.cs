using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SourceGenerators
{
    [Generator]
    public class AttributeGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            // No initialization required
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // Generate the attribute source code
            string attributeSource = @"
using System;

namespace IEnumerableUnpacker
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class UnpackableAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public class UnpackAttribute : Attribute
    {
        public string OutputName { get; }

        public UnpackAttribute(string outputName)
        {
            OutputName = outputName;
        }
    }
}
";

            // Add the generated source code to the compilation
            context.AddSource("IEnumerableUnpackerGeneratedAttributes.cs", SourceText.From(attributeSource, Encoding.UTF8));
        }
    }
}

