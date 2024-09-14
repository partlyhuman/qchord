namespace Partlyhuman.Qchord.Common;

enum CartType : byte
{
    SongCart = 0x55,
    RhythmCart = 0xAA,
}

public enum TimeSignature : byte
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