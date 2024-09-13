using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using static QcardToMidi.MidiEvent;

namespace QcardToMidi;

enum CartType : byte
{
    SongCart = 0x55,
    RhythmCart = 0xAA,
}

enum TimeSignature : byte
{
    ThreeFourTime = 0x90,
    FourFourTime = 0xC0,
}

enum MidiEvent : byte
{
    NotAnEvent = 0x7, // If first bit is 0, this is value/argument
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
    public static MidiEvent ToEventNibble(this byte b) => (MidiEvent)(b >> 4);

    public static bool IsEvent(this MidiEvent evt) => evt > NotAnEvent;
    
    public static int ArgumentLength(this MidiEvent evt) => evt switch
    {
        NoteOff => 1,
        ProgramChange => 1,
        ChannelPressure => 1,
        SystemExclusive => 1,
        _ => 2,
    };
}

struct Uint24(int value)
{
    private readonly byte high = (byte)((value >> 16) & 0xFF);
    private readonly byte middle = (byte)((value >> 8) & 0xFF);
    private readonly byte low = (byte)(value & 0xFF);

    public static implicit operator int(Uint24 pointer) =>
        (pointer.high << 16) | (pointer.middle << 8) | pointer.low;
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
        if (trackCount is < 0 or > 255)
        {
            throw new IndexOutOfRangeException($"Invalid track count {trackCount}");
        }

        timeSignatures = MemoryMarshal.Cast<byte, TimeSignature>(span[lengthPointer..])[..trackCount].ToArray();
        trackTempos = span[tempoPointer..][..trackCount].ToArray();
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

            if (b == 0xFF)
            {
                dt = null;
                evt = null;
                status = null;
                bytes = bytes[1..];
                continue;
            }

            MidiEvent eventNibble = b.ToEventNibble();
            if (eventNibble.IsEvent())
            {
                evt = eventNibble;
                status = b;
                bytes = bytes[1..];
            }

            if (evt == null || status == null)
            {
                throw new InvalidOperationException("Should have encountered a status byte by now");
            }

            // DEBUG Omit certain events from output, only for comparing to captured midi stream. Generally we don't want to silence anything
            bool muted = status is 0xB0 or 0xAA;
            
            // Write status and its argument bytes
            if (!muted) writer.Write(status.Value);
            int argc = evt.Value.ArgumentLength();
            if (!muted) writer.Write(bytes[..argc]);
            bytes = bytes[argc..];

            // Check for implicit velocity after NoteOff
            if (evt is NoteOff)
            {
                b = bytes[0];
                if (b.ToEventNibble().IsEvent())
                {
                    // next byte starts a new event: write implied velocity 0
                    writer.Write((byte)0);
                }
                else
                {
                    // next byte is a velocity: write and consume it
                    writer.Write(b);
                    bytes = bytes[1..];
                }
            }
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new($"[QCard type={type} tracks={trackCount}\n");
        for (int i = 0; i < trackCount; i++)
        {
            sb.AppendLine($" {i + 1,2}. tempo={trackTempos[i]:D3},{timeSignatures[i]} at 0x{(int)trackPointers[i]:X06}");
        }

        return sb.Append(']').ToString();
        // var ts = "[" + string.Join(", ", timeSignatures.Select(x => x.ToString())) + "]";
        // var te = "[" + string.Join(", ", trackTempos.Select(x => x.ToString())) + "]";
        // var tp = "[" + string.Join(", ", trackPointers.Select(x => ((int)x).ToString("X06"))) + "]";
        // return $"[QCard type={type} tracks={trackCount} time signatures={ts} tempos={te} track pointers={tp}]";
    }
}