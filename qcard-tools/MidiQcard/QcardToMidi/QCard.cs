using System.Buffers.Binary;
using System.Runtime.InteropServices;
using static QcardToMidi.MidiEvent;

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

struct Uint24(int value)
{
    private readonly byte high = (byte)((value >> 16) & 0xFF);
    private readonly byte middle = (byte)((value >> 8) & 0xFF);
    private readonly byte low = (byte)(value & 0xFF);

    public static implicit operator int(Uint24 pointer) =>
        (pointer.high << 16) | (pointer.middle << 8) | pointer.low;
}

enum MidiEvent : byte
{
    NotEvent = 0x7,
    NoteOff = 0x8,
    NoteOn = 0x9,
    KeyPressure = 0xA,
    ControlChange = 0xB,
    ProgramChange = 0xC,
    ChannelPressure = 0xD,
    PitchWheel = 0xE,
    SystemExclusive = 0xF,
}

static class MidiEventExtensions
{
    public static int ArgumentBytes(this MidiEvent evt) => evt switch
    {
        NoteOff => 1,
        ProgramChange => 1,
        ChannelPressure => 1,
        SystemExclusive => 1,
        _ => 2,
    };
}

public class QCard
{
    private readonly byte[] allBytes;
    private readonly CartType type;
    private readonly int trackCount;
    private readonly Uint24[] trackPointers;
    private readonly byte[] trackTempos;
    private readonly TimeSignature[] timeSignatures;

    public QCard(byte[] allBytes)
    {
        this.allBytes = allBytes;
        ReadOnlySpan<byte> span = allBytes.AsSpan();

        type = (CartType)span[0x5];

        int dataPointer = BinaryPrimitives.ReadUInt16BigEndian(span[0x20..0x22]);
        int tempoPointer = BinaryPrimitives.ReadUInt16BigEndian(span[0x22..0x24]);
        int lengthPointer = BinaryPrimitives.ReadUInt16BigEndian(span[0x24..0x26]);

        trackCount = tempoPointer - lengthPointer;
        timeSignatures = MemoryMarshal.Cast<byte, TimeSignature>(span[lengthPointer..tempoPointer]).ToArray();
        trackTempos = span[tempoPointer..dataPointer].ToArray();
        trackPointers = MemoryMarshal.Cast<byte, Uint24>(span[dataPointer..])[..trackCount].ToArray();
    }

    public void ConvertToMidiStreamNoTimes(int trackNum, Stream stream)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(trackNum);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(trackNum, trackCount);
        ArgumentNullException.ThrowIfNull(stream);

        ReadOnlySpan<byte> bytes = allBytes.AsSpan(trackPointers[trackNum]);
        using BinaryWriter writer = new(stream);

        // states
        int? dt = null;
        byte? status = null;
        MidiEvent? evt = null;
        while (bytes.Length > 0)
        {
            byte b = bytes[0];

            if (b == 0xFE)
            {
                bytes = [];
                continue;
            }

            if (dt == null)
            {
                dt = b;
                bytes = bytes[1..];
                continue;
            }

            if (bytes.Length >= 3 && bytes[..3].SequenceEqual<byte>([0xB0, 0x2C, 0x7F]))
            {
                bytes = bytes[3..];
                continue;
            }

            if (b == 0xFF)
            {
                dt = null;
                evt = null;
                status = null;
                bytes = bytes[1..];
                continue;
            }

            MidiEvent eventNibble = (MidiEvent)(b >> 4);
            if (eventNibble > NotEvent)
            {
                evt = eventNibble;
                status = b;
                bytes = bytes[1..];
            }

            if (evt == null || status == null)
            {
                throw new InvalidOperationException("Should have encountered a status byte by now");
            }

            writer.Write(status.Value);

            int argc = evt.Value.ArgumentBytes();
            writer.Write(bytes[..argc]);
            bytes = bytes[argc..];

            // Peek, if a note off is followed by note (already consumed) and then:
            // a new event: write velocity 0
            // a value: continue and write it as the velocity
            if (evt is NoteOff)
            {
                b = bytes[0];
                if ((MidiEvent)(b >> 4) <= NotEvent)
                {
                    writer.Write(b);
                    bytes = bytes[1..];
                }
                else
                {
                    writer.Write((byte)0);
                }
            }
        }
    }

    public override string ToString()
    {
        var ts = "[" + string.Join(", ", timeSignatures.Select(x => x.ToString())) + "]";
        var te = "[" + string.Join(", ", trackTempos.Select(x => x.ToString("X02"))) + "]";
        var tp = "[" + string.Join(", ", trackPointers.Select(x => ((int)x).ToString("X06"))) + "]";
        return $"[QCard type={type} tracks={trackCount} time signatures={ts} tempos={te} track pointers={tp}]";
    }
}