using static Partlyhuman.Qchord.Common.MidiStatus;

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

    public static TimeSignature FromFraction(int n, int d) => (n, d) switch
    {
        (3, 4) => TimeSignature.ThreeFourTime,
        (4, 4) => TimeSignature.FourFourTime,
        _ => throw new NotImplementedException($"Time signature {n}/{d} unsupported"),
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
    PitchBend = 0xE,
    SystemExclusive = 0xF,
}

public enum MidiMetaEvent : byte
{
    EndOfTrack = 0x2F,
    Tempo = 0x51,
    TimeSignature = 0x58,
}

public static class MidiStatusExtensions
{
    public static MidiStatus ToStatusNibble(this byte b) => (MidiStatus)(b >> 4);
    public static byte ToChannelNibble(this byte b) => (byte)(b & 0xF);

    public static bool IsStatus(this MidiStatus evt) => evt > NotAnEvent;
    public static bool IsStatus(this byte status) => status >= 0x80; // Was >

    public static bool IsChannelReservedQchord(this byte channelOrStatus)
    {
        // 1 reserved for melody keyboard
        // 2, 4 unknown currently
        // 14, 15, 16 reserved for strum plate
        return ((channelOrStatus & 0xF) + 1) is 1 or 2 or 4 or >= 14;
    }

    public static int ArgumentLengthQchord(this MidiStatus evt) => evt switch
    {
        NoteOff => 1,
        ProgramChange => 1,
        ChannelPressure => 1,
        SystemExclusive => 1,
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