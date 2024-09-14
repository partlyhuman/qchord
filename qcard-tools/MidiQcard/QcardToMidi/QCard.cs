using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

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

static class TimeSignatureExtensions
{
    public static (byte numerator, byte denominator) ToFraction(this TimeSignature ts) => ts switch
    {
        // denominator is log2(4)
        TimeSignature.ThreeFourTime => (3, 2),
        TimeSignature.FourFourTime => (4, 2),
        _ => throw new ArgumentOutOfRangeException(nameof(ts), ts, null),
    };
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

    public static bool IsEvent(this MidiEvent evt) => evt > MidiEvent.NotAnEvent;

    public static int ArgumentLength(this MidiEvent evt) => evt switch
    {
        MidiEvent.NoteOff => 1,
        MidiEvent.ProgramChange => 1,
        MidiEvent.ChannelPressure => 1,
        MidiEvent.SystemExclusive => 1,
        _ => 2,
    };
}

struct Uint24BigEndian(int value)
{
    private readonly byte high = (byte)((value >> 16) & 0xFF);
    private readonly byte middle = (byte)((value >> 8) & 0xFF);
    private readonly byte low = (byte)(value & 0xFF);

    public static implicit operator int(Uint24BigEndian value) =>
        (value.high << 16) | (value.middle << 8) | value.low;
}

public class QCard
{
    private readonly byte[] allBytes;
    private readonly CartType type;
    private readonly int trackCount;
    private readonly Uint24BigEndian[] trackPointers;
    private readonly byte[] trackTempos;
    private readonly TimeSignature[] timeSignatures;

    public int TrackCount => trackCount;

    public QCard(byte[] allBytes)
    {
        this.allBytes = allBytes;
        ReadOnlySpan<byte> span = allBytes.AsSpan();

        type = (CartType)span[0x5];
        trackCount = span[0x10] + 1;
        if (trackCount is < 0 or > 255)
        {
            throw new IndexOutOfRangeException($"Invalid track count {trackCount}");
        }

        int dataPointer = BinaryPrimitives.ReadUInt16BigEndian(span[0x20..0x22]);
        int tempoPointer = BinaryPrimitives.ReadUInt16BigEndian(span[0x22..0x24]);
        int lengthPointer = BinaryPrimitives.ReadUInt16BigEndian(span[0x24..0x26]);

        timeSignatures = MemoryMarshal.Cast<byte, TimeSignature>(span[lengthPointer..])[..trackCount].ToArray();
        trackTempos = span[tempoPointer..][..trackCount].ToArray();
        trackPointers = MemoryMarshal.Cast<byte, Uint24BigEndian>(span[dataPointer..])[..trackCount].ToArray();
    }

    public void ConvertToMidiFile(BinaryWriter fileWriter, int trackNum)
    {
        // Write header
        const int tickdiv = 48;
        Span<byte> headerBytes = [0, 0, 0, 1, 0, 0]; // format = 0 (single track midi), tracks = 1
        BinaryPrimitives.WriteUInt16BigEndian(headerBytes[4..], tickdiv); // 48 PPQN
        WriteAsChunk(fileWriter, headerBytes, "MThd");

        // Write midi track into memory instead of to file
        byte[] midiBuffer = new byte[allBytes.Length * 2]; // No way a single track will expand to twice the size of the entire ROM 
        using MemoryStream midiStream = new(midiBuffer);
        using BinaryWriter midiWriter = new(midiStream);

        // Write tempo meta
        midiWriter.Write((byte)0);
        Span<byte> tempoEvent = [0xff, 0x51, 0x03, 0, 0, 0];
        int microsPerQuarterNote = 20_000 * (trackTempos[trackNum] + 10);
        MemoryMarshal.Write(tempoEvent[3..], new Uint24BigEndian(microsPerQuarterNote));
        midiWriter.Write(tempoEvent);

        // Write time signature meta
        midiWriter.Write((byte)0);
        (byte n, byte d) = timeSignatures[trackNum].ToFraction();
        Span<byte> timeSignatureEvent = [0xFF, 0x58, 0x04, n, d, tickdiv, 8];
        midiWriter.Write(timeSignatureEvent);

        ConvertToMidiStream(midiWriter, trackNum, writeTimes: true, suppressSpecials: false);

        // Write end of track meta
        midiWriter.Write((byte)0);
        midiWriter.Write([0xFF, 0x2F, 0x00]);

        WriteAsChunk(fileWriter, midiBuffer.AsSpan(0, (int)midiStream.Position), "MTrk");
    }

    private void WriteAsChunk(BinaryWriter writer, ReadOnlySpan<byte> data, string id)
    {
        Span<byte> id4 = stackalloc byte[4];
        Encoding.ASCII.GetBytes(id.AsSpan(0, Math.Min(id.Length, id4.Length)), id4);

        Span<byte> len4 = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(len4, (uint)data.Length);

        writer.Write(id4);
        writer.Write(len4);
        writer.Write(data);
    }

    public void ConvertToMidiStream(BinaryWriter writer, int trackNum, bool writeTimes = true, bool suppressSpecials = false)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentOutOfRangeException.ThrowIfNegative(trackNum);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(trackNum, trackCount);

        ReadOnlySpan<byte> bytes = allBytes.AsSpan(trackPointers[trackNum]);

        // states
        byte? dt = null;
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

            if (evt == null || status == null || dt == null)
            {
                throw new InvalidOperationException("Should have encountered a status byte by now");
            }

            // If desired, omit certain events from output - many instances of B02C7F and AAXXXX
            bool suppress = suppressSpecials && status is 0xB0 or 0xAA;

            // Write first timestamp and then for this run all other events happen at same time
            if (!suppress && writeTimes) writer.Write(dt.Value);
            dt = 0;

            // Write status and its argument bytes
            if (!suppress) writer.Write(status.Value);
            int argc = evt.Value.ArgumentLength();
            if (!suppress) writer.Write(bytes[..argc]);
            bytes = bytes[argc..];

            // Check for implicit velocity after NoteOff
            if (evt is MidiEvent.NoteOff)
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