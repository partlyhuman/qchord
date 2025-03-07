using Partlyhuman.Qchord.Common;

namespace Partlyhuman.Qchord.Tests;

[TestFixture]
public static class VariableLengthQuantitiesTests
{
    private static readonly (UInt32 value, byte[] encoding)[] TestCases =
    [
        (0x00000000, [0x00]),
        (0x00000040, [0x40]),
        (0x0000007F, [0x7F]),
        (0x00000080, [0x81, 0x00]),
        (0x00002000, [0xC0, 0x00]),
        (0x00003FFF, [0xFF, 0x7F]),
        (0x00004000, [0x81, 0x80, 0x00]),
        (0x00100000, [0xC0, 0x80, 0x00]),
        (0x001FFFFF, [0xFF, 0xFF, 0x7F]),
        (0x00200000, [0x81, 0x80, 0x80, 0x00]),
        (0x08000000, [0xC0, 0x80, 0x80, 0x00]),
        (0x0FFFFFFF, [0xFF, 0xFF, 0xFF, 0x7F]),
    ];

    [TestCaseSource(nameof(TestCases))]
    public static void VariableLengthQuantitiesReadCorrectly((UInt32, byte[]) input)
    {
        var (actualValue, encoding) = input;
        Console.WriteLine($"Parsing {Convert.ToHexString(encoding)}, expect to read {actualValue}");
        (uint value, int bytesConsumed) result = MidiFileReader.ReadVariableLengthQuantity(encoding);
        Assert.That(result.value, Is.EqualTo(actualValue));
        Assert.That(result.bytesConsumed, Is.EqualTo(encoding.Length));
    }

    [TestCaseSource(nameof(TestCases))]
    public static void VariableLengthQuantitiesWriteCorrectly((UInt32, byte[]) input)
    {
        var (actualValue, encoding) = input;
        Console.WriteLine($"Converting {actualValue}, expect to write {Convert.ToHexString(encoding)}");
        ReadOnlySpan<byte> output = MidiFileReader.WriteVariableLengthQuantity(actualValue);
        CollectionAssert.AreEqual(encoding, output.ToArray());
        // Assert.That(output.SequenceEqual(encoding));
    }
}