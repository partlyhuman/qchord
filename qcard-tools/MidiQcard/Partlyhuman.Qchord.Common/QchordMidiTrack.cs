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
        tempoMicrosPerQuarterNote = (int)(60_000_000f / bpm);
        timeSignature = ts;
    }

    // Assuming chunk header stripped off
    public void ParseMidiTrack(byte[] source, int tickdiv)
    {
        throw new NotImplementedException();
    }

    public bool ValidateMidiTrack(byte[] source)
    {
        throw new NotImplementedException();
    }
}