using System.Diagnostics;
using System.Runtime.InteropServices;
using static System.Text.Encoding;

namespace _98bdecomp;

public static class Utils
{
    public static unsafe T ReadStruct<T>(Stream stream) where T : unmanaged
    {
        Span<byte> buffer = stackalloc byte[sizeof(T)];
        ReadSpan(stream, ref buffer);
        return MemoryMarshal.Read<T>(buffer);
    }
    public static unsafe T ReadStruct<T>(Span<byte> buffer, int start) where T : unmanaged
    {
        Span<byte> span2 = buffer.Slice(start, sizeof(T));
        return MemoryMarshal.Read<T>(span2);
    }

    public static void ReadSpan(Stream stream, ref Span<byte> buffer)
    {
        Debug.Assert(buffer.Length == stream.Read(buffer), "Unexpected end of stream");
    }
}

public unsafe struct RiffHeader
{
    private fixed byte id[4];
    public UInt32 Length;
    
    public static int Size => sizeof(RiffHeader);
    public string Id
    {
        get
        {
            fixed (byte* idPtr = id) return ASCII.GetString(idPtr, 4);
        }
    }
}

ref struct RiffChunk
{
    public RiffHeader Header;
    public Span<byte> Data;

    public RiffChunk(Span<byte> span, ref int offset)
    {
        Header = MemoryMarshal.Read<RiffHeader>(span.Slice(offset, RiffHeader.Size));
        offset += RiffHeader.Size;
        Data = span.Slice(offset, offset + (int)Header.Length);
        offset += (int)Header.Length;
    }
}

public struct InstrumentTableData
{
    public UInt16 ProgramNumber;
    public byte VariationNumber;
    public byte ParamPageNumber;
    public UInt16 ParamPagePointer;
}

public unsafe struct PriorityData
{
    private fixed byte name[8];
    public UInt16 Priority;
    
    public string Name
    {
        get
        {
            fixed (byte* namePtr = name) return ASCII.GetString(namePtr, 8);
        }
    }
}