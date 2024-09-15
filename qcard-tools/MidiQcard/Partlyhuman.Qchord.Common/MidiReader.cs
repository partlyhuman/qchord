using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;

namespace Partlyhuman.Qchord.Common;

public class MidiReader
{
    public const string MThd = "MThd";
    public const string MTrk = "MTrk";

    private readonly byte[] allBytes;
    private readonly Range headerRange;
    private readonly Range trackRange;

    public ReadOnlySpan<byte> GetHeaderData() => allBytes[headerRange];
    public ReadOnlySpan<byte> GetTrackData() => allBytes[trackRange];

    public MidiReader(byte[] allBytes)
    {
        this.allBytes = allBytes;
        int i = 0;
        while (ReadChunk(allBytes, ref i) is var (range, id))
        {
            if (id == MThd) headerRange = range;
            if (id == MTrk) trackRange = range;
        }
    }

    private (Range, string id)? ReadChunk(ReadOnlySpan<byte> span, ref int index)
    {
        if (index >= span.Length) return null;

        ReadOnlySpan<byte> idBytes = span[index..][..4];
        Span<char> idChars = stackalloc char[idBytes.Length];
        Encoding.ASCII.GetChars(idBytes, idChars);
        string id = new string(idChars);

        int length = BinaryPrimitives.ReadInt32BigEndian(span[(index + 4)..]);
        Range dataRange = (index + 8)..(index + 8 + length);

        index = dataRange.End.Value;
        return (dataRange, id);
    }

    public static ReadOnlySpan<byte> ReadMidiEvent(ReadOnlySpan<byte> span)
    {
        Debug.Assert(span[1].ToStatusNibble().IsStatus(), "Expected first byte to be a status");
        
        // TODO XXX this method is naive, does not work
        int argc = span[2..].IndexOfAnyInRange<byte>(0x80, 0xFF) - 2;
        // Could be the last, return everything 
        if (argc < 0)
        {
            return span;
        }

        Debug.Assert(argc <= 3, $"Unexpected argument length of {argc}");

        return span[..(2 + argc)];
    }

    public static ReadOnlySpan<byte> ConsumeMidiEvent(ref ReadOnlySpan<byte> span, out byte dt, out byte status, out MidiStatus statusNibble,
        out ReadOnlySpan<byte> argumentBytes)
    {
        ReadOnlySpan<byte> eventSpan = ReadMidiEvent(span);
        dt = span[0];
        status = span[1];
        statusNibble = status.ToStatusNibble();
        argumentBytes = eventSpan[2..];
        span = span[eventSpan.Length..];
        return eventSpan;
    }
    
}