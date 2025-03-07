using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Text.Encoding;

namespace _98bdecomp;

static class Utils
{
    public static T ReadStruct<T>(this BinaryReader reader) where T : struct
    {
        // OK to go on stack because we'll copy by value
        Span<byte> buffer = stackalloc byte[Marshal.SizeOf<T>()];
        if (reader.Read(buffer) != buffer.Length)
        {
            throw new EndOfStreamException();
        }

        return MemoryMarshal.Read<T>(buffer);
    }
}

unsafe struct RiffHeader
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

unsafe struct PriorityData
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

readonly struct InstrumentTableData
{
    public readonly UInt16 ProgramNumber;
    public readonly byte VariationNumber;
    public readonly byte ParamPageNumber;
    public readonly UInt16 ParamPagePointer;
}

record struct InstrumentSplitDefinitionParsed(byte StartNote, byte StartDyn, byte EndNote, byte EndDyn, bool Repeat);

readonly struct InstrumentSplitDefinition
{
    public readonly UInt16 SplitStartData;
    public readonly UInt16 SplitStopData;
    public readonly UInt16 SoundPointer;

    public byte Note(UInt16 data) => (byte)(data >> 8);
    public byte Dyn(UInt16 data) => (byte)(data | 0b111111);

    public bool Repeat(UInt16 data) => (data & (1 << 7)) != 0;
    public bool Mn(UInt16 data) => (data & (1 << 15)) != 0;
    public bool IsEnd() => Mn(SplitStopData);

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

        data = data[(i * Marshal.SizeOf<InstrumentSplitDefinition>())..];
        return all[..i];
    }

    public override string ToString() =>
        $"Split {Note(SplitStartData)}/{Dyn(SplitStartData)}...{Note(SplitStopData)}/{Dyn(SplitStopData)} @ 0x{SoundPointer:x04}";
}

readonly struct Split1
{
    private readonly UInt16 PtrtabPointer;

    private readonly UInt16 Status;

    public int CountEnvelopes() => ((Status >> 5) & 0b11) switch
    {
        0b11 => 1, 0b10 => 2, 0b01 => 3, 0b00 => 4,
        _ => throw new ArgumentOutOfRangeException("Eg nb"),
    };

    public int CountModulators() => ((Status >> 3) & 0b11) switch
    {
        0b10 => 0, 0b01 => 1, 0b00 => 2,
        _ => throw new ArgumentOutOfRangeException("Mod nb"),
    };

    public int CountKeyboardTables() => (Status & 0b111) switch
    {
        0b100 => 0, 0b011 => 1, 0b010 => 2, 0b001 => 3, 0b000 => 4,
        _ => throw new ArgumentOutOfRangeException("Kbd nb"),
    };

    public override string ToString()
    {
        return $"Ptrtab {PtrtabPointer:x04}\n" +
               $"Status {Status:b016}\n" +
               $"{CountKeyboardTables()} Keyboard tables, {CountEnvelopes()} Envelopes, {CountModulators()} Modulators";
    }

    private const int SizeofWord = 2;

    public int GetObjectsStructOffset() =>
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
}

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

    public override string ToString()
    {
        return $"ObjectCounts {bitfield:b016}\n" +
               $"{CountMA1()} MA1, {CountMA2()} MA2, {CountMB()} MB, {CountMX()} MX, {CountMY()} MY";
    }
}

readonly struct MA1
{
    private readonly UInt16 field0;
    private readonly UInt16 field1;
    private readonly UInt16 field2;

    public override string ToString()
    {
        return $"{nameof(MA1)} {field0:b016} {field1:b016} {field2:b016}";
    }
}

// VARIABLE SIZE, NOT STRUCT
record MA2(UInt16 Field0, UInt16 Field1, UInt16? Field2 = null)
{
    public bool IsFreqType() => (Field1 & 0b1) == 0;

    public static MA2 ReadDynamic(BinaryReader input)
    {
        UInt16 field0 = input.ReadUInt16();
        UInt16 field1 = input.ReadUInt16();
        if ((field1 & 0b1) == 0)
        {
            // Typ = 0 to indicate MA2 frequency object
            UInt16 field2 = input.ReadUInt16();
            return new MA2(field0, field1, field2);
        }
        else
        {
            return new MA2(field0, field1);
        }
    }

    public override string ToString()
    {
        return $"{nameof(MA2)} {Field0:b016} {Field1:b016} {Field2:b016}";
    }
}

readonly struct MB
{
    private readonly UInt16 field0;
    private readonly UInt16 field1;

    public override string ToString()
    {
        return $"{nameof(MB)} {field0:b016} {field1:b016}";
    }
}

readonly struct MX
{
    private readonly UInt16 field0;

    public override string ToString()
    {
        return $"{nameof(MX)} {field0:b016}";
    }
}

// Dynamic size
record MY(UInt16 Field0, UInt16? Field1 = null)
{
    private readonly UInt16 xferField0;
    private readonly UInt16 amplitudeField0;

    private readonly UInt16 amplitudeField1;
    // TYP field determines whether it's a 

    public static MY ReadDynamic(BinaryReader input)
    {
        UInt16 field0 = input.ReadUInt16();
        if ((field0 & 0b1) == 0)
        {
            // Typ = 0 to indicate MY Xfer type (done)
            return new MY(field0);
        }
        else
        {
            UInt16 field1 = input.ReadUInt16();
            return new MY(field0, field1);
        }
    }

    public override string ToString()
    {
        return $"{nameof(MY)} {Field0:b016} {Field1:b016}";
    }
}

readonly struct Modulator
{
    private readonly UInt16 field0;
    private readonly UInt16 field1;

    public override string ToString()
    {
        return $"{nameof(Modulator)} {field0:b016} {field1:b016}";
    }
}

record Envelope(string Description)
{
    public struct Header
    {
        public readonly UInt16 Field0;
    }

    public struct Segment
    {
        public readonly UInt16 Field0;
        public readonly UInt16 Field1;
    }

    public static Envelope ReadDynamic(BinaryReader input)
    {
        UInt16 header = input.ReadUInt16();

        int i = 0;
        for (; ; i++)
        {
            // if (i >= 8)
            // {
            //     // throw new InvalidOperationException("More than 8 segments in envelope");
            //     break;
            // }
            
            UInt16 segment = input.ReadUInt16();
            int code = (segment >> 13);

            if (code is < 0 or > 4)
            {
                throw new IndexOutOfRangeException($"Expected code 0-4, found {code}");
            }
            
            if (code == 0)
            {
                continue;
            }

            if (code == 1)
            {
                UInt16 jumpBack = input.ReadUInt16();
            }

            // Only 0 continues
            break;
        }

        return new Envelope($"Envelope with {i} segments");
    }
}