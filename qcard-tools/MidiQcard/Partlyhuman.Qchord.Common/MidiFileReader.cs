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
    //
    // /// Only concerned with consuming the correct number of bytes, does not move any indexes or return any further data
    // public static ReadOnlySpan<byte> ReadMidiEvent(ReadOnlySpan<byte> span, byte? runningStatus)
    // {
    //     if (span.IsEmpty) return [];
    //
    //     byte status = (span[1].IsStatus() ? span[1] : runningStatus) ?? throw new InvalidOperationException("No running status, but not status byte");
    //
    //     // if (!statusNibble.IsStatus()) throw new InvalidOperationException("Expected first byte to be a status");
    //
    //     int argc = status.ToStatusNibble().ArgumentLengthMidi() ?? status switch
    //     {
    //         0xF7 or 0xF0 => span[2] + 1,
    //         0xFF => span[3] + 2,
    //         _ => throw new InvalidOperationException($"Status byte {span[1]} invalid in a MIDI file"),
    //     };
    //
    //     return span[..(2 + argc)];
    // }

    public static ReadOnlySpan<byte> ConsumeMidiEvent(ref ReadOnlySpan<byte> span, ref byte? status, out byte dt,
        out MidiStatus statusNibble, out ReadOnlySpan<byte> argumentBytes, out byte metaEventType)
    {
        if (span.IsEmpty) throw new ArgumentOutOfRangeException();

        // Full event length including timestamp
        int len = 1;
        dt = span[0];

        // Running status - replace existing or continue running
        status = span[1].IsStatus() ? span[1] : status;
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
                argumentBytes = span[2..4]; // 2 bytes
                len = 4;
                break;
            case (MidiStatus.ProgramChange, _):
            case (MidiStatus.ChannelPressure, _):
                argumentBytes = span[2..3]; // 1 byte
                len = 3;
                break;
            case (MidiStatus.SystemExclusive, 0xF7 or 0xF0):
                // length in third byte DT 0xF0 LEN ARGS...
                argc = span[2];
                len = 3 + argc;
                argumentBytes = span[3..len];
                break;
            case (MidiStatus.SystemExclusive, 0xFF):
                // length in fourth byte DT 0xFF TYPE LEN ARGS...
                metaEventType = span[2];
                argc = span[3];
                len = 4 + argc;
                argumentBytes = span[4..len];
                break;
            default:
                throw new InvalidOperationException($"Status byte {status} invalid in a MIDI file");
        }

        ReadOnlySpan<byte> evt = span[..len];
        span = span[len..];
        return evt;
    }
}