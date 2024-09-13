using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    
    public void ConvertToMidiStreamNoTimes(int trackNum, Stream stream, BinaryReader? verifyReader)
    {
        if (trackNum >= trackCount) throw new ArgumentOutOfRangeException(nameof(trackNum));
        ReadOnlySpan<byte> bytes = allBytes.AsSpan(trackPointers[trackNum]);
        using BinaryWriter writer = new(stream);

        // states
        int? dt = null;
        byte? status = null;
        MidiEvent? evt = null;
        while (bytes.Length > 0)
        {
            byte b = bytes[0];

            if (b == 0xfe)
            {
                return;
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

            if (b == 0xff)
            {
                dt = null;
                evt = null;
                status = null;
                bytes = bytes[1..];
                continue;
            }

            MidiEvent byteAsEvent = (MidiEvent)(b >> 4);
            if (byteAsEvent > NotEvent)
            {
                evt = byteAsEvent;
                status = b;
                bytes = bytes[1..];
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

    public override string ToString() => $"QCard Type: {type} Tracks: {trackCount} Tempos: {trackTempos}";
}

enum MidiEvent : byte
{
    NotEvent = 0b0111,
    NoteOff = 0b1000,
    NoteOn = 0b1001,
    KeyPressure = 0b1010,
    ControlChange = 0b1011,
    ProgramChange = 0b1100,
    ChannelPressure = 0b1101,
    PitchWheel = 0b1110,
    ChannelMode = 0b1011,
    SystemExclusive = 0b1111,
    QChord = 0xA,
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