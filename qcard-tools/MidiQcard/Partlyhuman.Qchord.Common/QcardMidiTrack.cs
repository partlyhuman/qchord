using System.Numerics;
using System.Text;
using static System.Buffers.Binary.BinaryPrimitives;
using static System.Runtime.InteropServices.MemoryMarshal;
using static Partlyhuman.Qchord.Common.Logger;
using static Partlyhuman.Qchord.Common.MidiStatus;

namespace Partlyhuman.Qchord.Common;

/// <summary>
/// Container for Qcard track data. Can convert from real MIDI.
/// </summary>
public class QcardMidiTrack
{
    public const int TickDiv = 48;
    public const int MicrosPerMinute = 60_000_000;
    public const int DefaultTempoBpm = 120;
    public const TimeSignature DefaultTimeSignature = TimeSignature.FourFourTime;
    public static readonly byte[] EndMarker = [0xFF, 0xFE, 0xFE, 0xFE, 0xFE];

    private readonly byte[] allBytes;
    private readonly double tickDivMultiplier = 1;
    private TimeSignature? timeSignature;
    private int? tempoMicrosPerQuarterNote;

    public ReadOnlySpan<byte> AsSpan() => allBytes;
    internal int TempoMicrosPerQuarterNote => tempoMicrosPerQuarterNote ?? (MicrosPerMinute / DefaultTempoBpm);
    internal TimeSignature TimeSignature => timeSignature ?? DefaultTimeSignature;

    /// Use existing Qcard track
    public QcardMidiTrack(ReadOnlySpan<byte> raw, int? tempoMPQN = null, TimeSignature ts = DefaultTimeSignature)
    {
        // Make copy of bytes up to and including the first EOT marker
        allBytes = raw[..(raw.IndexOf(EndMarker) + EndMarker.Length)].ToArray();
        tempoMicrosPerQuarterNote = tempoMPQN ?? MicrosPerMinute / DefaultTempoBpm;
        timeSignature = ts;
    }

    // New Qcard track converted from MIDI file
    public QcardMidiTrack(MidiFileReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        UInt16 midiTickDiv = ReadUInt16BigEndian(reader.GetHeaderData()[4..6]);
        if (midiTickDiv != TickDiv)
        {
            tickDivMultiplier = (double)TickDiv / midiTickDiv;
            Console.WriteLine(
                $"WARNING: Mismatching MIDI tickdiv={midiTickDiv} QChord tickdiv={TickDiv}, will scale delta-times by {tickDivMultiplier:N2}. Possible loss of accuracy.");
        }

        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        FromMidi(reader, writer);
        allBytes = stream.GetBuffer()[..(int)stream.Position];

        int length = allBytes.Length;
        int midiLen = reader.GetTrackData().Length;
        Console.WriteLine($"Input MIDI {midiLen:N0} bytes -> QCard {length:N0} bytes ({(float)length / midiLen:P0})");
    }

    // MIDI -> Qcard
    private void FromMidi(MidiFileReader reader, BinaryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(writer);
        ReadOnlySpan<byte> midiData = reader.GetTrackData();
        MidiParseWarnings warnings = new();

        bool firstEventInSpan = true;
        byte? status = null;
        while (midiData.Length > 0)
        {
            byte? lastStatus = status;
            ReadOnlySpan<byte> eventBytes = MidiFileReader.ConsumeMidiEvent(ref midiData, ref status, out uint dt,
                out MidiStatus statusNibble, out ReadOnlySpan<byte> argumentBytes, out byte metaEventType);

            // Log("< " + Convert.ToHexString(eventBytes));
            // Log("> ", false);

            if (status == null) throw new InvalidOperationException("Status should be set");

            warnings.Check(status.Value, statusNibble);

            if (statusNibble is SystemExclusive)
            {
                switch ((MidiMetaEvent)metaEventType)
                {
                    case MidiMetaEvent.EndOfTrack:
                        midiData = [];
                        writer.Write(EndMarker);
                        break;
                    case MidiMetaEvent.Tempo:
                        tempoMicrosPerQuarterNote = Read<Uint24BigEndian>(argumentBytes);
                        // Log($"tempo={tempoMicrosPerQuarterNote} multiplier={tickDivMultiplier} bpm={MicrosPerMinute / tempoMicrosPerQuarterNote}");
                        // tempoMicrosPerQuarterNote = (int)(tempoMicrosPerQuarterNote * tickdivMultiplier);
                        // Log($"tempo_adjusted={tempoMicrosPerQuarterNote}");
                        break;
                    case MidiMetaEvent.TimeSignature when argumentBytes is [var n, var d, var tickdiv, var b]:
                        timeSignature = TimeSignatureExtensions.FromFraction(n, 1 << d);
                        // Log($"time signature tickdiv={tickdiv} b={b}");
                        // if (tickdiv != QCard.TickDiv)
                        // {
                        //     tickdivMultiplier = (double)QCard.TickDiv / tickdiv;
                        //     Log($"Using tickdiv multiplier of {tickdivMultiplier} to match MIDI tickdiv of {tickdiv}");
                        // }

                        break;
                    default:
                        Log($"Ignoring unrecognized meta event 0x{metaEventType:X02}");
                        break;
                }

                // ignore meta events
                status = null;
                // Log("");
                continue;
            }

            if (dt != 0 && !firstEventInSpan)
            {
                writer.Write((byte)0xFF);
                firstEventInSpan = true;
            }

            if (firstEventInSpan)
            {
                // CONVERT DT FROM MIDI RESOLUTION TO QCARD RESOLUTION
                var dtAdjusted = (UInt32)(dt * tickDivMultiplier);

                // IF qchord supports variable length times
                // ReadOnlySpan<byte> dtBytes = MidiFileReader.WriteVariableLengthQuantity(dtAdjusted);
                // LOG($"{dt} -> {dt:X} -> {Convert.ToHexString(dtBytes)}");
                // writer.Write(dtBytes);

                // NOTE: I *assume* qchord does not support variable length times
                if (dtAdjusted >= 0x80)
                {
                    throw new InvalidOperationException($"Adjusted delta-time of {dtAdjusted} (originally {dt}) is too large to fit in one byte." +
                                                        $"Qcard does not support variable length delta-time.");
                }

                writer.Write((byte)dtAdjusted);
                writer.Write(status.Value);
                firstEventInSpan = false;
            }
            else
            {
                // Omit 0 dt
                if (status == lastStatus /*&& statusNibble is NoteOn or NoteOff*/)
                {
                    // allow running status
                }
                else
                {
                    writer.Write(status.Value);
                }
            }

            if (statusNibble is NoteOff && argumentBytes.Length == 2 && argumentBytes[1] == 0)
            {
                // Not allowed to combine running status with omitted off velocity
                argumentBytes = argumentBytes[..1];
                status = null;
            }

            // This would be incorrect in cases like sysex but we're not writing those out
            writer.Write(argumentBytes);
            // Log("");
        }

        warnings.Check(tempoMicrosPerQuarterNote, timeSignature);

        Console.WriteLine(warnings);
    }

    // QCard -> Midi
    public void WriteMidiStream(BinaryWriter writer, bool writeTimes = true, bool suppressSpecials = false)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ReadOnlySpan<byte> span = AsSpan();

        // states
        byte? dt = null;
        byte? status = null;
        MidiStatus? evt = null;
        while (span.Length > 0)
        {
            byte b = span[0];

            if (b == 0xFE)
            {
                span = [];
                continue;
            }

            if (dt == null)
            {
                dt = b;
                span = span[1..];
                continue;
            }

            if (b == 0xFF)
            {
                dt = null;
                evt = null;
                status = null;
                span = span[1..];
                continue;
            }

            MidiStatus statusNibble = b.ToStatusNibble();
            if (statusNibble.IsStatus())
            {
                evt = statusNibble;
                status = b;
                span = span[1..];
            }

            if (evt == null || status == null || dt == null)
            {
                throw new InvalidOperationException("Should have encountered a status byte by now");
            }

            // If desired, omit Qchord specific metronome and chord events
            bool suppress = suppressSpecials && status is 0xB0 or 0xAA;

            // Write first timestamp and then for this run all other events happen at same time
            if (!suppress && writeTimes) writer.Write(dt.Value);
            dt = 0;

            // Write status and its argument bytes
            if (!suppress) writer.Write(status.Value);
            int argc = evt.Value.ArgumentLengthQchord();
            if (!suppress) writer.Write(span[..argc]);
            span = span[argc..];

            // Check for implicit velocity after NoteOff
            if (evt is NoteOff)
            {
                b = span[0];
                if (b.IsStatus())
                {
                    // next byte starts a new event: write implied velocity 0
                    writer.Write((byte)0);
                }
                else
                {
                    // next byte is a velocity: write and consume it
                    writer.Write(b);
                    span = span[1..];
                }
            }
        }
    }

    public void WriteMidiFile(BinaryWriter fileWriter)
    {
        // Write header
        Span<byte> headerBytes = [0, 0, 0, 1, 0, 0]; // format = 0 (single track midi), tracks = 1
        WriteUInt16BigEndian(headerBytes[4..], TickDiv); // 48 PPQN
        Chunk.WriteChunk(fileWriter, headerBytes, Chunk.MidiHeader);

        // Write midi track into memory instead of to file
        using MemoryStream midiStream = new();
        using BinaryWriter midiWriter = new(midiStream);

        // Write tempo meta
        midiWriter.Write((byte)0);
        Span<byte> tempoEvent = [0xFF, 0x51, 0x03, 0, 0, 0];
        Write(tempoEvent[3..], new Uint24BigEndian(TempoMicrosPerQuarterNote));
        midiWriter.Write(tempoEvent);

        // Write time signature meta
        midiWriter.Write((byte)0);
        (byte n, byte d) = TimeSignature.ToFraction();
        Span<byte> timeSignatureEvent = [0xFF, 0x58, 0x04, n, (byte)BitOperations.Log2(d), TickDiv, 8];
        midiWriter.Write(timeSignatureEvent);

        // NOTE TOTALLY FAKED CMaj! Needed for MIDI players?
        // Write Key Signature meta
        midiWriter.Write((byte)0);
        midiWriter.Write([0xFF, 0x59, 0x02, 0, 0]);

        WriteMidiStream(midiWriter, writeTimes: true, suppressSpecials: false);

        // Write end of track meta
        midiWriter.Write((byte)0);
        midiWriter.Write([0xFF, 0x2F, 0x00]);

        ReadOnlySpan<byte> midiBytes = midiStream.GetBuffer().AsSpan(0, (int)midiStream.Position);
        Chunk.WriteChunk(fileWriter, midiBytes, Chunk.MidiTrack);
    }
}

class MidiParseWarnings
{
    private readonly HashSet<byte> invalidChannelUsages = new();
    private bool hasChordData = false;
    private bool hasMetronomeTicks = false;
    private bool hasTempo = false;
    private bool hasTimeSignature = false;

    public void Check(byte status, MidiStatus statusNibble)
    {
        if (statusNibble is NoteOff or NoteOn && status.IsChannelReservedQchord())
        {
            invalidChannelUsages.Add(status.ToChannelNibble());
        }

        if (status is 0xAA)
        {
            hasChordData = true;
        }

        if (status is 0xB0)
        {
            hasMetronomeTicks = true;
        }
    }

    public void Check(int? tempo, TimeSignature? ts)
    {
        if (tempo.HasValue) hasTempo = true;
        if (ts.HasValue) hasTimeSignature = true;
    }

    public override string ToString()
    {
        StringBuilder sb = new();
        if (invalidChannelUsages.Any())
        {
            sb.AppendLine($"WARNING: Notes written to reserved channels {string.Join(',', invalidChannelUsages.Order())}");
        }

        if (!hasChordData)
        {
            sb.AppendLine("WARNING: Missing Qchord chord data!");
        }

        if (!hasMetronomeTicks)
        {
            sb.AppendLine("WARNING: Missing Qchord metronome ticks!"); // TODO add programmatically
        }

        if (!hasTempo)
        {
            sb.AppendLine($"WARNING: Missing tempo meta, assuming {QcardMidiTrack.DefaultTempoBpm} bpm.");
        }

        if (!hasTimeSignature)
        {
            sb.AppendLine($"WARNING: Missing time signature meta, assuming {QcardMidiTrack.DefaultTimeSignature}");
        }

        return sb.ToString();
    }
}