using System.Diagnostics;

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