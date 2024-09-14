using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace Partlyhuman.Qchord.Common;

public class QCard
{
    public const int MaxSize = 0x80000; // max with A0-18
    public const int TickDiv = 48;

    private readonly byte[] allBytes;
    private readonly CartType type;
    private readonly int trackCount;
    private readonly Uint24BigEndian[] trackStartPointers;
    private readonly byte[] tempos;
    private readonly TimeSignature[] timeSignatures;

    public int TrackCount => trackCount;
    public byte[] Bytes => allBytes;

    /// Parse Qcard from ROM
    public QCard(byte[] allBytes)
    {
        this.allBytes = allBytes;
        ReadOnlySpan<byte> span = allBytes.AsSpan();

        type = (CartType)span[0x5];
        trackCount = span[0x10] + 1;
        if (trackCount is < 0 or > 99) // 2-digit LCD
        {
            throw new IndexOutOfRangeException($"Invalid track count {trackCount}");
        }

        int dataPointer = BinaryPrimitives.ReadUInt16BigEndian(span[0x20..0x22]);
        int tempoPointer = BinaryPrimitives.ReadUInt16BigEndian(span[0x22..0x24]);
        int timeSignaturePointer = BinaryPrimitives.ReadUInt16BigEndian(span[0x24..0x26]);

        timeSignatures = MemoryMarshal.Cast<byte, TimeSignature>(span[timeSignaturePointer..])[..trackCount].ToArray();
        tempos = span[tempoPointer..][..trackCount].ToArray();
        trackStartPointers = MemoryMarshal.Cast<byte, Uint24BigEndian>(span[dataPointer..])[..trackCount].ToArray();
    }

    /// Create new QCard from tracks
    public QCard(QchordMidiTrack[] tracks, int romSize = MaxSize)
    {
        allBytes = new byte[romSize];
        type = CartType.SongCart;
        trackCount = tracks.Length;
        Span<byte> span = allBytes.AsSpan();

        // Alloc then fill out
        trackStartPointers = new Uint24BigEndian[trackCount];
        tempos = new byte[trackCount];
        timeSignatures = new TimeSignature[trackCount];
        
        int headerDataPointer = 0x30;
        int trackStartPointer = headerDataPointer +
                               trackCount * (sizeof(TimeSignature) + Marshal.SizeOf<Uint24BigEndian>() + sizeof(byte));
        
        for (int i = 0; i < trackCount; i++)
        {
            QchordMidiTrack track = tracks[i];
            tempos[i] = (byte)(track.TempoMicrosPerQuarterNote / 20_000 - 10);
            timeSignatures[i] = track.TimeSignature;
            
            // Round up to nearest 16
            trackStartPointer = (trackStartPointer + 0xF) & ~0xF;
            trackStartPointers[i] = new Uint24BigEndian(trackStartPointer);
            ReadOnlySpan<byte> trackData = track.AsSpan();
            trackData.CopyTo(span[trackStartPointer..]);
            // Console.WriteLine($"Placing track {i} length {trackData.Length} at {trackStartPointer:X06}");
            trackStartPointer += trackData.Length;
        }
        
        // Done modifying header, write it
        span[0x5] = (byte)type;
        span[0x10] = (byte)(trackCount - 1);
        
        // These appear in these order usually
        // TIMESIG, TEMPO, DATA
        UInt16 timeSignaturePointer = (UInt16)headerDataPointer;
        ReadOnlySpan<byte> timeSignatureBytes = MemoryMarshal.Cast<TimeSignature,byte>(timeSignatures);
        timeSignatureBytes.CopyTo(span[timeSignaturePointer..]);
        BinaryPrimitives.WriteUInt16BigEndian(span[0x24..0x26], timeSignaturePointer);
        headerDataPointer += timeSignatureBytes.Length;

        UInt16 tempoPointer = (UInt16)headerDataPointer;
        // Span<byte> tempoBytes = tempos.AsSpan();
        tempos.CopyTo(span[tempoPointer..]);
        BinaryPrimitives.WriteUInt16BigEndian(span[0x22..0x24], tempoPointer);
        headerDataPointer += tempos.Length;

        UInt16 dataPointer = (UInt16)headerDataPointer;
        ReadOnlySpan<byte> trackStartPointersBytes = MemoryMarshal.Cast<Uint24BigEndian, byte>(trackStartPointers);
        trackStartPointersBytes.CopyTo(span[dataPointer..]);
        BinaryPrimitives.WriteUInt16BigEndian(span[0x20..0x22], dataPointer);
    }
    
    public void TrackDataToMidiFile(BinaryWriter fileWriter, int trackNum)
    {
        // Write header
        Span<byte> headerBytes = [0, 0, 0, 1, 0, 0]; // format = 0 (single track midi), tracks = 1
        BinaryPrimitives.WriteUInt16BigEndian(headerBytes[4..], TickDiv); // 48 PPQN
        WriteChunk(fileWriter, headerBytes, "MThd");

        // Write midi track into memory instead of to file
        byte[] midiBuffer = new byte[allBytes.Length * 2]; // No way a single track will expand to twice the size of the entire ROM 
        using MemoryStream midiStream = new(midiBuffer);
        using BinaryWriter midiWriter = new(midiStream);

        // Write tempo meta
        midiWriter.Write((byte)0);
        Span<byte> tempoEvent = [0xff, 0x51, 0x03, 0, 0, 0];
        int microsPerQuarterNote = 20_000 * (tempos[trackNum] + 10);
        MemoryMarshal.Write(tempoEvent[3..], new Uint24BigEndian(microsPerQuarterNote));
        midiWriter.Write(tempoEvent);

        // Write time signature meta
        midiWriter.Write((byte)0);
        (byte n, byte d) = timeSignatures[trackNum].ToFraction();
        Span<byte> timeSignatureEvent = [0xFF, 0x58, 0x04, n, d, TickDiv, 8];
        midiWriter.Write(timeSignatureEvent);

        TrackDataToMidiStream(midiWriter, trackNum, writeTimes: true, suppressSpecials: false);

        // Write end of track meta
        midiWriter.Write((byte)0);
        midiWriter.Write([0xFF, 0x2F, 0x00]);

        WriteChunk(fileWriter, midiBuffer.AsSpan(0, (int)midiStream.Position), "MTrk");
    }

    private void WriteChunk(BinaryWriter writer, ReadOnlySpan<byte> data, string id)
    {
        Span<byte> id4 = stackalloc byte[4];
        Encoding.ASCII.GetBytes(id.AsSpan(0, Math.Min(id.Length, id4.Length)), id4);

        Span<byte> len4 = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(len4, (uint)data.Length);

        writer.Write(id4);
        writer.Write(len4);
        writer.Write(data);
    }

    public void TrackDataToMidiStream(BinaryWriter writer, int trackNum, bool writeTimes = true, bool suppressSpecials = false)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentOutOfRangeException.ThrowIfNegative(trackNum);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(trackNum, trackCount);

        ReadOnlySpan<byte> bytes = allBytes.AsSpan(trackStartPointers[trackNum]);

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
            sb.AppendLine($" {i + 1,2}. tempo={tempos[i]:D3},{timeSignatures[i]} at 0x{(int)trackStartPointers[i]:X06}");
        }

        return sb.Append(']').ToString();
        // var ts = "[" + string.Join(", ", timeSignatures.Select(x => x.ToString())) + "]";
        // var te = "[" + string.Join(", ", trackTempos.Select(x => x.ToString())) + "]";
        // var tp = "[" + string.Join(", ", trackPointers.Select(x => ((int)x).ToString("X06"))) + "]";
        // return $"[QCard type={type} tracks={trackCount} time signatures={ts} tempos={te} track pointers={tp}]";
    }
}