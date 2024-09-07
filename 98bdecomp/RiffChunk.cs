using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace _98bdecomp;

public static class Utils
{
    public static unsafe T ReadStruct<T>(Stream stream) where T : unmanaged
    {
        Span<byte> buffer = stackalloc byte[sizeof(T)];
        ReadSpan(stream, ref buffer);
        return MemoryMarshal.AsRef<T>(buffer);
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
    
    public string Id
    {
        get
        {
            fixed (byte* idPtr = id) return Encoding.ASCII.GetString(idPtr, 4);
        }
    }
}


public unsafe struct PrioChunk
{
    private fixed byte name[8];
    public UInt16 Priority;
    public string Name
    {
        get
        {
            fixed (byte* namePtr = name) return Encoding.ASCII.GetString(namePtr, 8);
        }
    }

    
}