using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Text.Encoding;

namespace _98bdecomp;

public unsafe struct RiffHeader
{
    private fixed byte id[4];
    public UInt32 Length;

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
        int len = Marshal.SizeOf<RiffHeader>();
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

public record struct InstrumentSplitDefinitionParsed(byte StartNote, byte StartDyn, byte EndNote, byte EndDyn, bool Repeat);

public readonly struct InstrumentSplitDefinition
{
    public readonly UInt16 SplitStartData;
    public readonly UInt16 SplitStopData;
    public readonly UInt16 SoundPointer;

    private byte Note(UInt16 data) => (byte)(data >> 8);
    private byte Dyn(UInt16 data) => (byte)(data | 0b111111);

    private bool Repeat(UInt16 data) => (data & (1 << 7)) != 0;
    private bool Mn(UInt16 data) => (data & (1 << 15)) != 0;
    private bool IsEnd() => Mn(SplitStopData);

    public InstrumentSplitDefinitionParsed Parse() => new(
        StartNote: Note(SplitStartData),
        StartDyn: Dyn(SplitStopData),
        EndNote: Note(SplitStopData),
        EndDyn: Dyn(SplitStopData),
        Repeat: Repeat(SplitStartData));


    public static ReadOnlySpan<InstrumentSplitDefinition> TakeN(ref Span<byte> data)
    {
        var all = MemoryMarshal.Cast<byte, InstrumentSplitDefinition>(data);
        int i = 0;

        for (;; i++)
        {
            var current = all[i];
            if (current.IsEnd())
            {
                break;
            }
        }

        data = data[..(i * Marshal.SizeOf<InstrumentSplitDefinition>())];
        return all[..i];
    }

    public override string ToString() =>
        $"Inst Split from note {Note(SplitStartData)} / dyn {Dyn(SplitStartData)} -> note {Note(SplitStopData)} / dyn {Dyn(SplitStopData)} @ 0x{SoundPointer:x04} repeat? {Repeat(SplitStartData)}";
}

public struct Split
{
    private UInt16 PtrtabPointer;

    private UInt16 Status;

    private int CountEnvelopes() => ((Status >> 5) & 0b11) switch
    {
        0b11 => 1, 0b10 => 2, 0b01 => 3, 0b00 => 4,
        _ => throw new ArgumentOutOfRangeException("Eg nb"),
    };


    private int CountModulators() => ((Status >> 3) & 0b11) switch
    {
        0b10 => 0, 0b01 => 1, 0b00 => 2,
        _ => throw new ArgumentOutOfRangeException("Mod nb"),
    };

    private int CountKeyboardTables() => (Status & 0b111) switch
    {
        0b100 => 0, 0b011 => 1, 0b010 => 2, 0b001 => 3, 0b000 => 4,
        _ => throw new ArgumentOutOfRangeException("Kbd nb"),
    };

    private const int SizeofWord = 2;

    private int GetObjectsStructOffset() =>
        SizeofWord // Ptrtab
        + SizeofWord // Status
        + CountKeyboardTables() * SizeofWord
        + CountEnvelopes() * SizeofWord
        + CountModulators() * SizeofWord;
    /*
     *
       Ptrtab pointer (1 word)
       Status (1 word)
       Kbd pointers (0 to 4 words) (Kbd = keyboard tracking)
       Eg pointers (1 to 4 words) (Eg = Envelope Generator)
       Mod pointers (0 to 2 words) (Mod = modulator)
       Objects number (1 word)
       Ma1 objects definition
       Ma2 objects definition
       Mb objects definition
       Mx objects definition
       My objects definition
       Modulator definitions
       Enveloppe generator definitions
       Keyboard table definitions
       Ptrpab table definitions
     */

    readonly struct ObjectCounts
    {
        private readonly UInt16 bitfield;        
        public static int ToInt3Bit(int b) => (~b & 0b111) + 1;
        public static int ToInt2Bit(int b) => (~b & 0b11) + 1;

        public int CountMA1() => ToInt2Bit(bitfield >> 14);
        public int CountMA2() => ToInt3Bit(bitfield >> 11);
        public int CountMB() => ToInt3Bit(bitfield >> 8);
        public int CountMX() => ToInt3Bit(bitfield >> 5);
        public int CountMY() => ToInt3Bit(bitfield >> 2);
    }

    readonly struct MA1
    {
        private readonly UInt16 field0;
        private readonly UInt16 field1;
        private readonly UInt16 field2;
    }
    readonly struct MA2
    {
        private readonly UInt16 field0;
        private readonly UInt16 field1;
        private readonly UInt16 field2;
        // MA2 xfer object???
    }
    readonly struct MB
    {
        private readonly UInt16 field0;
        private readonly UInt16 field1;
    }
    readonly struct MX
    {
        private readonly UInt16 field0;
    }
    readonly struct MY
    {
        private readonly UInt16 xferField0;
        private readonly UInt16 amplitudeField0;
        private readonly UInt16 amplitudeField1;
        // TYP field determines whether it's a 
    }
}