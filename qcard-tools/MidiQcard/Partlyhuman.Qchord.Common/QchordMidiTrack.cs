using System.Buffers.Binary;
using System.ComponentModel;
using System.Numerics;

namespace Partlyhuman.Qchord.Common;

public class QchordMidiTrack
{
    private readonly byte[] bytes;
    private int length = 0;

    private TimeSignature? timeSignature;
    private int? tempoMicrosPerQuarterNote;

    internal int TempoMicrosPerQuarterNote => tempoMicrosPerQuarterNote!.Value;
    internal TimeSignature TimeSignature => timeSignature!.Value;

    public ReadOnlySpan<byte> AsSpan() => bytes.AsSpan(0, length);

    public QchordMidiTrack(int size)
    {
        bytes = new byte[size];
    }

    /// Create a from pre-existing data without converting anything. Usually use the primary constructor.
    public QchordMidiTrack(byte[] raw, int bpm = 120, TimeSignature ts = TimeSignature.FourFourTime)
    {
        bytes = raw;
        length = raw.Length;
        tempoMicrosPerQuarterNote = (int)(60_000_000L / bpm);
        timeSignature = ts;
    }

    public static QchordMidiTrack FromMidi(MidiReader midi)
    {
        QchordMidiTrack qchordTrack = new(midi.GetTrackData().Length);
        using MemoryStream stream = new(qchordTrack.bytes);
        using BinaryWriter writer = new(stream);
        qchordTrack.ConvertMidi(midi, writer);
        qchordTrack.length = (int)stream.Position;
        return qchordTrack;
    }

    private void ConvertMidi(MidiReader reader, BinaryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        HashSet<byte> invalidChannelUsages = new();
        ReadOnlySpan<byte> trackData = reader.GetTrackData();

        bool firstEventInSpan = true;
        byte? runningStatus = null;
        while (trackData.Length > 0)
        {
            ReadOnlySpan<byte> eventBytes =
                MidiReader.ConsumeMidiEvent(ref trackData, out byte dt, out byte status, out MidiStatus statusNibble, out var argumentBytes);
            
            Console.WriteLine(Convert.ToHexString(eventBytes));            

            if (statusNibble is MidiStatus.NoteOff or MidiStatus.NoteOn && status.IsChannelReserved())
            {
                invalidChannelUsages.Add(status.ToChannelNibble());
            }

            // TODO parse tempo and time signature metas - probably do this beforehand as its own loop
            this.timeSignature = TimeSignature.FourFourTime;
            this.tempoMicrosPerQuarterNote = (int)(60_000_000L / 120);

            if (statusNibble is MidiStatus.SystemExclusive)
            {
                // ignore meta events
                continue;
            }

            // TODO convert dt

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

            if (statusNibble is MidiStatus.NoteOff && argumentBytes.Length == 2 && argumentBytes[1] == 0)
            {
                argumentBytes = argumentBytes[..1];
            }

            writer.Write(argumentBytes);
        }

        if (invalidChannelUsages.Any())
        {
            Console.WriteLine($"WARNING: Notes written to reserved channels {string.Join(',', invalidChannelUsages)}");
        }
        // Other warnings...?
    }
}