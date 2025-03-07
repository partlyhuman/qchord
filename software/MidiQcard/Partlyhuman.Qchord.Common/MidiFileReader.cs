namespace Partlyhuman.Qchord.Common;

/// <summary>
/// Container and barebones event stream parser for real midi files
/// </summary>
public class MidiFileReader
{
    private readonly byte[] allBytes;
    private readonly Range? headerRange;
    private readonly Range? trackRange;

    public ReadOnlySpan<byte> GetHeaderData() => allBytes[headerRange ?? Range.All];
    public ReadOnlySpan<byte> GetTrackData() => allBytes[trackRange ?? Range.All];

    public MidiFileReader(byte[] allBytes)
    {
        this.allBytes = allBytes;
        int index = 0;
        while (Chunk.IdentifyAndConsumeChunk(allBytes, ref index) is var (chunkDataRange, id))
        {
            switch (id)
            {
                case Chunk.MidiHeader:
                    headerRange = chunkDataRange;
                    break;
                case Chunk.MidiTrack:
                    if (trackRange != null)
                    {
                        Console.WriteLine("WARNING: only support single-track MIDI, using first track");
                    }

                    trackRange ??= chunkDataRange;
                    break;
                default:
                    Console.WriteLine($"WARNING: Unimplemented chunk type {id}, ignoring");
                    break;
            }
        }

        if (trackRange == null) throw new InvalidOperationException("Did not find track data");
        if (headerRange == null) throw new InvalidOperationException("Did not find header data");
    }

    private static readonly byte[] VariableLengthBuffer = new byte[4];

    /// Signed return is safe since this only gets up to 7 * 4 = 28 bits
    public static (UInt32 value, int bytesConsumed) ReadVariableLengthQuantity(ReadOnlySpan<byte> span)
    {
        const int maxBytes = 4;
        const byte highBitMask = 1 << 7;

        // Last byte will have MSB 0
        var length = 1 + span.IndexOfAnyInRange<byte>(0x00, 0x7F);

        if (length == 1)
        {
            return (span[0], length);
        }

        if (length > maxBytes)
        {
            throw new InvalidOperationException($"Reached {maxBytes} bytes without encountering high bit");
        }

        ReadOnlySpan<byte> temp = span[..length];

        int acc = 0;
        for (int i = 0; i < length; i++)
        {
            acc |= (temp[^(i + 1)] & ~highBitMask) << (7 * i);
        }

        return ((UInt32)acc, length);
    }

    public static ReadOnlySpan<byte> WriteVariableLengthQuantity(UInt32 value)
    {
        const byte highBitMask = 1 << 7;

        Span<byte> bytes = VariableLengthBuffer;
        if (value < 0x80)
        {
            bytes[0] = (byte)value;
            return bytes[..1];
        }

        bytes.Clear();

        int i = 0;
        for (; value > 0; i++)
        {
            byte element = (byte)(value & ~highBitMask);
            value >>= 7;
            bytes[i] = element;
        }

        bytes = bytes[..i];
        bytes.Reverse();

        // Sure we can do this during the loop but this works
        for (i = 0; i < bytes.Length - 1; i++)
        {
            bytes[i] |= 0x80;
        }

        return bytes;
    }

    public static ReadOnlySpan<byte> ConsumeMidiEvent(ref ReadOnlySpan<byte> span, ref byte? status, out uint dt,
        out MidiStatus statusNibble, out ReadOnlySpan<byte> argumentBytes, out byte metaEventType)
    {
        if (span.IsEmpty) throw new ArgumentOutOfRangeException();

        int len;
        int argStart;

        // Full event length including timestamp
        var (value, statusStart) = ReadVariableLengthQuantity(span);
        dt = value;

        // Running status - replace existing or continue running
        if (span[statusStart].IsStatus())
        {
            status = span[statusStart];
            argStart = statusStart + 1;
        }
        else
        {
            // keep existing status
            argStart = statusStart;
        }

        if (status == null) throw new InvalidOperationException("No running status, but not status byte");

        // if (!statusNibble.IsStatus()) throw new InvalidOperationException("Expected first byte to be a status");

        statusNibble = status.Value.ToStatusNibble();
        metaEventType = default;
        int argc;
        switch (statusNibble, status)
        {
            case (MidiStatus.NoteOff, _):
            case (MidiStatus.NoteOn, _):
            case (MidiStatus.KeyPressure, _):
            case (MidiStatus.ControlChange, _):
            case (MidiStatus.PitchBend, _):
                // 2 bytes
                argc = 2;
                len = argStart + argc;
                argumentBytes = span[argStart..len];
                break;
            case (MidiStatus.ProgramChange, _):
            case (MidiStatus.ChannelPressure, _):
                // 1 byte
                argc = 1;
                len = argStart + argc;
                argumentBytes = span[argStart..len];
                break;
            case (MidiStatus.SystemExclusive, 0xF7 or 0xF0):
                // status followed by length then data
                // DT 0xF0 LEN ARGS...
                argc = span[argStart];
                len = argStart + 1 + argc;
                argumentBytes = span[(argStart + 1)..len];
                break;
            case (MidiStatus.SystemExclusive, 0xFF):
                // status followed by subtype then length then data
                // DT 0xFF TYPE LEN ARGS...
                metaEventType = span[argStart];
                argc = span[argStart + 1];
                len = argStart + 2 + argc;
                argumentBytes = span[(argStart + 2)..len];
                break;
            default:
                throw new InvalidOperationException($"Status byte {status} invalid in a MIDI file");
        }

        ReadOnlySpan<byte> evt = span[..len];
        span = span[len..];
        return evt;
    }

    public long SumDuration()
    {
        ReadOnlySpan<byte> span = GetTrackData();
        long duration = 0;
        byte? status = null;
        while (!span.IsEmpty)
        {
            ConsumeMidiEvent(ref span, ref status, out uint dt, out _, out _, out _);
            duration += dt;
        }

        return duration;
    }
}