using System.Buffers.Binary;
using System.Text;

namespace Partlyhuman.Qchord.Common;

public static class Chunk
{
    // Chunk ids
    public const string MidiHeader = "MThd";
    public const string MidiTrack = "MTrk";

    /// <summary>
    /// Don't allocate or copy chunk, but find the range in the provided span of the actual data.
    /// For use in a while loop.
    /// </summary>
    /// <param name="span">Source data</param>
    /// <param name="index">Start index. Will be moved forward to the next byte after the chunk ends</param>
    /// <returns>null if no chunk found, otherwise the range of the chunk DATA, and its name</returns>
    public static (Range chunkDataRange, string id)? IdentifyAndConsumeChunk(ReadOnlySpan<byte> span, ref int index)
    {
        if (span.IsEmpty || index >= span.Length) return null;

        ReadOnlySpan<byte> idBytes = span[index..][..4];
        Span<char> idChars = stackalloc char[idBytes.Length];
        Encoding.ASCII.GetChars(idBytes, idChars);
        string id = new string(idChars);

        int length = BinaryPrimitives.ReadInt32BigEndian(span[(index + 4)..]);
        Range dataRange = (index + 8)..(index + 8 + length);

        index = dataRange.End.Value;
        return (dataRange, id);
    }

    /// <summary>
    /// Write data as a chunk using a writer
    /// </summary>
    public static void WriteChunk(BinaryWriter writer, ReadOnlySpan<byte> data, string id)
    {
        Span<byte> id4 = stackalloc byte[4];
        Encoding.ASCII.GetBytes(id.AsSpan(0, Math.Min(id.Length, id4.Length)), id4);

        Span<byte> len4 = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(len4, (uint)data.Length);

        writer.Write(id4);
        writer.Write(len4);
        writer.Write(data);
    }
}