# IEnumerableUnpacker 📦

![](https://i.imgur.com/nDFt7M0.png)

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/asieradzk/IEnumerableUnpacker/blob/master/LICENSE.txt)
[![NuGet](https://img.shields.io/nuget/v/ArrayUnpacker.svg)](https://www.nuget.org/packages/IEnumerableUnpacker)

IEnumerableUnpacker is a C# library that provides a quick and efficient way to unpack `IEnumerable<T>` to 1D and 2D arrays of T's members. It leverages parallel processing and optimized memory copying techniques to achieve high-performance array unpacking. 🚀

## Key Features

- Unpack `IEnumerable<T>` to 1D and 2D arrays of T's members
- Utilize parallel processing for individual members ⚡
- Identify blittable types and use unaligned memory copy for optimized performance
- Support for generic types
- Flexible attribute-based configuration for specifying output parameter names

## Installation

You can install IEnumerableUnpacker via NuGet Package Manager:
```
Install-Package IEnumerableUnpacker
```
## Usage

Here's an example of how to use IEnumerableUnpacker:

```csharp
[Unpackable]
public class UnpackMe<Titem, Titem2, UselessGeneric>
{
    [Unpack("MyItegersOut")]
    public int[] myIntegers;

    [Unpack("MyIntegerOut")]
    public int myInteger;

    [Unpack("MyFloatsOut")]
    public float[] myFloats;

    [Unpack("MyGenericOut")]
    public Titem[] myGeneric;

    [Unpack("MyGeneric2Out")]
    public Titem2 myGeneric2;
}

public static unsafe void UnpackUnpackMe<Titem, Titem2, UselessGeneric>(this IEnumerable<UnpackMe<Titem, Titem2, UselessGeneric>> source, out int[,] MyItegersOut, out int[] MyIntegerOut, out float[,] MyFloatsOut, out Titem[,] MyGenericOut, out Titem2[] MyGeneric2Out)
{
    // Unpacking logic...
}

```

In this example:

-   The `Unpackable` attribute is used to mark the class for unpacking.
-   The `Unpack` attribute is used to specify the output parameter names for the unpacked arrays.
-   Generic types are supported and can be used as needed.
-   Parameters not labeled with the `Unpack` attribute will not be unpacked.

For more detailed benchmarks and comparisons, please visit the [project repository](https://github.com/asieradzk/IEnumerableUnpacker). 📊

## Benchmarks
Benchmarks and template for generated source is aviable in [Benchmark repository](https://github.com/asieradzk/UnpackBenchmarks)


## Contributing

Contributions are welcome! If you find any issues or have suggestions for improvements, please open an issue or submit a pull request on the [GitHub repository](https://github.com/asieradzk/IEnumerableUnpacker). 😊

## License

IEnumerableUnpacker is licensed under the [MIT License](https://github.com/asieradzk/IEnumerableUnpacker/blob/master/LICENSE.txt).

## Note

I've decided to use 2D arrays because that's what I use with TorchSharp. Unpacking to flat types might be faster, so feel free to reach out to me if it's required! 📫

