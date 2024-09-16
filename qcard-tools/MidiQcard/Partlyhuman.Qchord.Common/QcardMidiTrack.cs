using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using static Partlyhuman.Qchord.Common.Logger;
using static Partlyhuman.Qchord.Common.MidiStatus;

namespace Partlyhuman.Qchord.Common;

/// <summary>
/// Container for Qcard track data. Can convert from real MIDI.
/// </summary>
public class QcardMidiTrack
{
    private const int MicrosPerMinute = 60_000_000;
    public const int DefaultTempoBpm = 120;
    public const TimeSignature DefaultTimeSignature = TimeSignature.FourFourTime;

    private readonly byte[] bytes;
    private readonly double tickDivMultiplier = 1;
    private TimeSignature? timeSignature;
    private int? tempoMicrosPerQuarterNote;

    public ReadOnlySpan<byte> AsSpan() => bytes;
    internal int TempoMicrosPerQuarterNote => tempoMicrosPerQuarterNote ?? (MicrosPerMinute / DefaultTempoBpm);
    internal TimeSignature TimeSignature => timeSignature ?? DefaultTimeSignature;

    /// Use existing Qcard track data
    public QcardMidiTrack(byte[] raw, int bpm = DefaultTempoBpm, TimeSignature ts = DefaultTimeSignature)
    {
        bytes = raw;
        tempoMicrosPerQuarterNote = MicrosPerMinute / bpm;
        timeSignature = ts;
    }

    // Convert from MIDI file
    public QcardMidiTrack(MidiFileReader midi)
    {
        var midiTickDiv = BinaryPrimitives.ReadUInt16BigEndian(midi.GetHeaderData()[4..6]);
        tickDivMultiplier = (double)QCard.TickDiv / midiTickDiv;
        // Log($"midi tickdiv={midiTickDiv} multiplier={tickDivMultiplier}");

        int midiLen = midi.GetTrackData().Length;
        bytes = new byte[midiLen * 2];
        using MemoryStream stream = new(bytes);
        using BinaryWriter writer = new(stream);
        MidiToQcardTrackData(midi, writer);
        int length = (int)stream.Position;
        Array.Resize(ref bytes, length);

        Console.WriteLine($"Input MIDI {midiLen:N0} bytes -> QCard {length:N0} bytes ({(float)length / midiLen:P0})");
    }

    private void MidiToQcardTrackData(MidiFileReader reader, BinaryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ReadOnlySpan<byte> trackData = reader.GetTrackData();
        MidiParseWarnings warnings = new();

        bool firstEventInSpan = true;
        byte? status = null;
        while (trackData.Length > 0)
        {
            byte? lastStatus = status;
            ReadOnlySpan<byte> eventBytes = MidiFileReader.ConsumeMidiEvent(ref trackData, ref status, out uint dt,
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
                        trackData = [];
                        writer.Write([0xFF, 0xFE, 0xFE, 0xFE, 0xFE]);
                        break;
                    case MidiMetaEvent.Tempo:
                        tempoMicrosPerQuarterNote = MemoryMarshal.Read<Uint24BigEndian>(argumentBytes);
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
                ReadOnlySpan<byte> dtBytes = MidiFileReader.WriteVariableLengthQuantity((UInt32)(dt * tickDivMultiplier));
                // LOG($"{dt} -> {dt:X} -> {Convert.ToHexString(dtBytes)}");
                writer.Write(dtBytes);
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
}

class MidiParseWarnings
{
    private HashSet<byte> invalidChannelUsages = new();
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