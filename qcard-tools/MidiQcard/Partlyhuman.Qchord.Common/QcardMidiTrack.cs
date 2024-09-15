using System.Text;
using static Partlyhuman.Qchord.Common.MidiStatus;

namespace Partlyhuman.Qchord.Common;

/// <summary>
/// Container for Qcard track data. Can convert from real MIDI.
/// </summary>
public class QcardMidiTrack
{
    public const int DefaultTempoBpm = 120;
    public const TimeSignature DefaultTimeSignature = TimeSignature.FourFourTime;

    private readonly byte[] bytes;
    private int length = 0;

    private TimeSignature? timeSignature;
    private int? tempoMicrosPerQuarterNote;

    internal int TempoMicrosPerQuarterNote => tempoMicrosPerQuarterNote!.Value;
    internal TimeSignature TimeSignature => timeSignature!.Value;

    public ReadOnlySpan<byte> AsSpan() => bytes.AsSpan(0, length);

    private QcardMidiTrack(int size)
    {
        bytes = new byte[size];
    }

    /// Use existing Qcard track data
    public QcardMidiTrack(byte[] raw, int bpm = DefaultTempoBpm, TimeSignature ts = DefaultTimeSignature)
    {
        bytes = raw;
        length = raw.Length;
        tempoMicrosPerQuarterNote = (int)(60_000_000L / bpm);
        timeSignature = ts;
    }

    // Convert from MIDI file
    public QcardMidiTrack(MidiFileReader midi) : this(midi.GetTrackData().Length)
    {
        using MemoryStream stream = new(bytes);
        using BinaryWriter writer = new(stream);
        MidiToQcardTrackData(midi, writer);
        length = (int)stream.Position;
    }

    private void MidiToQcardTrackData(MidiFileReader reader, BinaryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ReadOnlySpan<byte> trackData = reader.GetTrackData();
        MidiParseWarnings warnings = new();

        bool firstEventInSpan = true;
        byte? runningStatus = null;
        while (trackData.Length > 0)
        {
            ReadOnlySpan<byte> eventBytes =
                MidiFileReader.ConsumeMidiEvent(ref trackData, out byte dt, out byte status, out MidiStatus statusNibble, out var argumentBytes);

            Console.WriteLine(Convert.ToHexString(eventBytes));

            warnings.Check(status, statusNibble, eventBytes);

            // TODO parse tempo and time signature metas - probably do this beforehand as its own loop
            timeSignature = TimeSignature.FourFourTime;
            tempoMicrosPerQuarterNote = (int)(60_000_000L / DefaultTempoBpm);

            if (statusNibble is SystemExclusive)
            {
                // ignore meta events
                continue;
            }

            // TODO convert dt if PPQN differ

            if (dt != 0 && !firstEventInSpan)
            {
                writer.Write((byte)0xFF);
                runningStatus = null;
                firstEventInSpan = true;
            }

            if (firstEventInSpan)
            {
                writer.Write(dt);
                firstEventInSpan = false;
            }

            if (status != runningStatus)
            {
                writer.Write(status);
                runningStatus = status;
            }

            if (statusNibble is NoteOff && argumentBytes.Length == 2 && argumentBytes[1] == 0)
            {
                argumentBytes = argumentBytes[..1];
            }

            writer.Write(argumentBytes);
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

    public void Check(byte status, MidiStatus statusNibble, ReadOnlySpan<byte> argumentBytes)
    {
        if (statusNibble is NoteOff or NoteOn && status.IsChannelReservedQchord())
        {
            invalidChannelUsages.Add(status.ToChannelNibble());
        }

        if (status is 0xAA)
        {
            hasChordData = true;
        }

        if (status is 0xB0 && argumentBytes.SequenceEqual<byte>([0x2C, 0x7F]))
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