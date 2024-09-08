using System.Diagnostics;
using System.Runtime.InteropServices;
using static System.Text.Encoding;

namespace _98bdecomp;

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
        int len = RiffHeader.Size;
        Header = MemoryMarshal.Read<RiffHeader>(span.Slice(offset, len));
        offset += len;

        len = (int)Header.Length;
        Data = span.Slice(offset, len);
        offset += len;
    }
}

public unsafe struct PriorityData
{
    private fixed byte name[8];
    public readonly UInt16 Priority;

    public string Name
    {
        get
        {
            fixed (byte* namePtr = name) return ASCII.GetString(namePtr, 8);
        }
    }
}

public readonly struct InstrumentTableData
{
    public readonly UInt16 ProgramNumber;
    public readonly byte VariationNumber;
    public readonly byte ParamPageNumber;
    public readonly UInt16 ParamPagePointer;
}

public readonly struct InstrumentSplit
{
    public readonly UInt16 SplitStartData;
    public readonly UInt16 SplitStopData;
    public readonly UInt16 SoundPointer;

    public byte Note(UInt16 data) => (byte)(data >> 8);
    public byte Dyn(UInt16 data) => (byte)(data | 0b111111);
    
    public bool Repeat(UInt16 data) => (data & (1 << 7)) != 0;
    public bool Mn(UInt16 data) => (data & (1 << 15)) != 0;

    public bool IsEnd() => Mn(SplitStopData);

    public static ReadOnlySpan<InstrumentSplit> TakeN(ReadOnlySpan<byte> data)
    {
        ReadOnlySpan<InstrumentSplit> all = MemoryMarshal.Cast<byte, InstrumentSplit>(data);
        int i = 0;
        
        for (;; i++)
        {
            InstrumentSplit currentInstrumentSplit = all[i];
            if (currentInstrumentSplit.IsEnd())
            {
                break;
            }
        }

        return all[..i];
    } 
    
    public override string ToString() => 
        $"Inst Split from note {Note(SplitStartData)} / dyn {Dyn(SplitStartData)} -> note {Note(SplitStopData)} / dyn {Dyn(SplitStopData)} @ 0x{SoundPointer:x04} repeat? {Repeat(SplitStartData)}";
}
