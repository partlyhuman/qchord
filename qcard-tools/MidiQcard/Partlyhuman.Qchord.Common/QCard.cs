using System.Runtime.InteropServices;
using System.Text;
using static System.Buffers.Binary.BinaryPrimitives;
using static System.Runtime.InteropServices.MemoryMarshal;

namespace Partlyhuman.Qchord.Common;

/// <summary>
/// Container for Qcard data. Converts both ways, can read midi from Qcard, or construct Qcard from converted MIDI tracks.
/// </summary>
public class QCard
{
    public const int MaxSize = 0x80000; // max with A0-18

    private readonly byte[] allBytes;
    private readonly CartType type;
    private readonly int trackCount;
    private readonly Uint24BigEndian[] trackStartPointers;
    private readonly byte[] tempos;
    private readonly TimeSignature[] timeSignatures;

    public int TrackCount => trackCount;
    public ReadOnlySpan<byte> AsSpan() => allBytes.AsSpan();

    public QcardMidiTrack this[int i] => new(
        // TODO: assumes track pointers monotonically increase
        raw: allBytes[(int)trackStartPointers[i]..(i + 1 < trackCount ? trackStartPointers[i + 1] : allBytes.Length)],
        tempoMPQN: TempoToMicrosPerQuarterNote(tempos[i]),
        ts: timeSignatures[i]
    );

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

        int dataPointer = ReadUInt16BigEndian(span[0x20..0x22]);
        int tempoPointer = ReadUInt16BigEndian(span[0x22..0x24]);
        int timeSignaturePointer = ReadUInt16BigEndian(span[0x24..0x26]);

        timeSignatures = Cast<byte, TimeSignature>(span[timeSignaturePointer..])[..trackCount].ToArray();
        tempos = span[tempoPointer..][..trackCount].ToArray();
        trackStartPointers = Cast<byte, Uint24BigEndian>(span[dataPointer..])[..trackCount].ToArray();
    }

    /// Create new QCard from tracks
    public QCard(QcardMidiTrack[] tracks, int romSize = MaxSize)
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
            QcardMidiTrack track = tracks[i];
            tempos[i] = (byte)(track.TempoMicrosPerQuarterNote / 20_000 - 10);
            timeSignatures[i] = track.TimeSignature;

            // Round up to nearest 0x100
            trackStartPointer = (trackStartPointer + 0xFF) & ~0xFF;

            ReadOnlySpan<byte> trackData = track.AsSpan();
            if (trackStartPointer + trackData.Length > romSize)
            {
                Console.WriteLine($"WARNING: No more room on cart, truncating to first {i} songs!\n");
                trackCount = i;
                Array.Resize(ref trackStartPointers, trackCount);
                Array.Resize(ref tempos, trackCount);
                Array.Resize(ref timeSignatures, trackCount);
                break;
            }

            trackStartPointers[i] = new Uint24BigEndian(trackStartPointer);
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
        ReadOnlySpan<byte> timeSignatureBytes = Cast<TimeSignature, byte>(timeSignatures);
        timeSignatureBytes.CopyTo(span[timeSignaturePointer..]);
        WriteUInt16BigEndian(span[0x24..0x26], timeSignaturePointer);
        headerDataPointer += timeSignatureBytes.Length;

        UInt16 tempoPointer = (UInt16)headerDataPointer;
        // Span<byte> tempoBytes = tempos.AsSpan();
        tempos.CopyTo(span[tempoPointer..]);
        WriteUInt16BigEndian(span[0x22..0x24], tempoPointer);
        headerDataPointer += tempos.Length;

        UInt16 dataPointer = (UInt16)headerDataPointer;
        ReadOnlySpan<byte> trackStartPointersBytes = Cast<Uint24BigEndian, byte>(trackStartPointers);
        trackStartPointersBytes.CopyTo(span[dataPointer..]);
        WriteUInt16BigEndian(span[0x20..0x22], dataPointer);
    }

    public static int TempoToMicrosPerQuarterNote(int t) => 20_000 * (t + 10);

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