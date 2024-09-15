namespace Partlyhuman.Qchord.Common;

public enum CartType : byte
{
    SongCart = 0x55,
    RhythmCart = 0xAA,
}

public enum TimeSignature : byte
{
    ThreeFourTime = 0x90,
    FourFourTime = 0xC0,
}

public static class TimeSignatureExtensions
{
    public static (byte numerator, byte denominator) ToFraction(this TimeSignature ts) => ts switch
    {
        TimeSignature.ThreeFourTime => (3, 4),
        TimeSignature.FourFourTime => (4, 4),
        _ => throw new ArgumentOutOfRangeException(nameof(ts), ts, null),
    };
}

public enum MidiStatus : byte
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

public static class MidiStatusExtensions
{
    public static MidiStatus ToStatusNibble(this byte b) => (MidiStatus)(b >> 4);
    public static byte ToChannelNibble(this byte b) => (byte)(b & 0xF);

    public static bool IsStatus(this MidiStatus evt) => evt > MidiStatus.NotAnEvent;

    public static bool IsChannelReserved(this byte channelOrStatus)
    {
        // 11 reserved for chords
        // 14, 15, 16 reserved for strum plate
        // 1 reserved for melody keyboard
        return (channelOrStatus & 0xF) is <= 1 or 11 or >= 14;
    }
    
    public static int ArgumentLength(this MidiStatus evt) => evt switch
    {
        MidiStatus.NoteOff => 1,
        MidiStatus.ProgramChange => 1,
        MidiStatus.ChannelPressure => 1,
        MidiStatus.SystemExclusive => 1,
        _ => 2,
    };
}

internal struct Uint24BigEndian(int value)
{
    private readonly byte high = (byte)((value >> 16) & 0xFF);
    private readonly byte middle = (byte)((value >> 8) & 0xFF);
    private readonly byte low = (byte)(value & 0xFF);

    public static implicit operator int(Uint24BigEndian value) =>
        (value.high << 16) | (value.middle << 8) | value.low;
}