using System.Runtime.InteropServices;

namespace QcardToMidi;

enum CartType : byte
{
    Song = 0x55,
    Rhythm = 0xAA,
}

enum TimeSignature : byte
{
    ThreeFour = 0x90,
    FourFour = 0xC0,
}

struct Uint24
{
    private byte high;
    private byte middle;
    private byte low;

    public static implicit operator int(Uint24 pointer)
    {
        return (pointer.high << 16) | (pointer.middle << 8) | pointer.low;
    }
}

public class QCard
{
    private byte[] allBytes;
    private readonly CartType type;
    private readonly int trackCount;
    private readonly int[] trackPointers;
    private readonly byte[] trackTempos;
    private readonly TimeSignature[] timeSignatures;

    public QCard(byte[] allBytes)
    {
        this.allBytes = allBytes;
        ReadOnlySpan<byte> span = allBytes.AsSpan();

        type = (CartType)span[0x5];
        int dataPointer = BitConverter.ToUInt16([span[0x21], span[0x20]]);
        int tempoPointer = BitConverter.ToUInt16([span[0x23], span[0x22]]);
        int lengthPointer = BitConverter.ToUInt16([span[0x25], span[0x24]]);

        trackCount = tempoPointer - lengthPointer;
        timeSignatures = MemoryMarshal.Cast<byte, TimeSignature>(span[lengthPointer..tempoPointer]).ToArray();
        trackTempos = span[tempoPointer..dataPointer].ToArray();
        ReadOnlySpan<byte> trackPointers8 = span[dataPointer..(dataPointer + 3 * trackCount)];
        ReadOnlySpan<Uint24> trackPointers24 = MemoryMarshal.Cast<byte, Uint24>(trackPointers8);
        trackPointers = trackPointers24.ToArray().Select(u24 => (int)u24).ToArray();
    }

    public void ConvertToMidiStreamNoTimes(int trackNum, Stream stream)
    {
        if (trackNum >= trackCount) throw new ArgumentOutOfRangeException(nameof(trackNum));
        ReadOnlySpan<byte> bytes = allBytes.AsSpan(trackPointers[trackNum]);
        using BinaryWriter writer = new(stream);

        // states
        int dt = 0;
        while (bytes.Length > 0)
        {
            byte b = bytes[0];

            if (bytes[..3] == [0xB0, 0x2C, 0x7F])
            {
                bytes = bytes[3..];
                continue;
            }

            switch (b)
            {
                case 0xfe:
                    // Done
                    bytes = bytes[..0];
                    break;
                case 0xff:
                    dt = bytes[1];
                    bytes = bytes[2..];
                    break;
                case 0xaa:
                    // do something with the next 2 bytes
                    bytes = bytes[3..];
                    break;
                case >= 0x80 and < 0x90:
                    // Note off
                    writer.Write(bytes[..2]);
                    writer.Write((byte)0);
                    bytes = bytes[2..];
                    break;
                case >= 0x90 and < 0xC0:
                    writer.Write(bytes[..3]);
                    bytes = bytes[3..];
                    break;
                case >= 0xC0 and < 0xE0:
                    // one byte argument
                    writer.Write(bytes[..2]);
                    bytes = bytes[2..];
                    break;
                case >= 0xE0 and < 0xF0:
                    // Pitch bend
                    writer.Write(bytes[..3]);
                    bytes = bytes[3..];
                    break;

                default:
                    bytes = bytes[1..];
                    Console.WriteLine($"Unhandled midi status 0x{b:x02}");
                    break;
            }
        }
    }

    public override string ToString() => $"QCard Type: {type} Tracks: {trackCount} Tempos: {trackTempos}";
}