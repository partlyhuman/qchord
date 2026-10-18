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

[StructLayout(LayoutKind.Sequential)]
readonly struct InstrumentTableData
{
    public readonly UInt16 ProgramNumber;
    public readonly byte VariationNumber;
    public readonly byte ParamPageNumber;
    public readonly UInt16 ParamPointer;

    public override string ToString() =>
        $"{nameof(InstrumentTableData)} program {ProgramNumber} (hex {ProgramNumber:x}) variation {VariationNumber} in PARA @ 0x{ParamPointer:x04}";
}

record struct InstrumentSplitDefinitionParsed(byte StartNote, byte StartDyn, byte EndNote, byte EndDyn, bool Repeat);

[StructLayout(LayoutKind.Sequential)]
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

    public static IEnumerable<InstrumentSplitDefinition> ReadAllSplits(BinaryReader reader)
    {
        while (true)
        {
            var splitDef = reader.ReadStruct<InstrumentSplitDefinition>();
            Console.WriteLine(splitDef);
            yield return splitDef;
            if (splitDef.IsEnd()) yield break;
        }
    }

    public override string ToString() =>
        $"Split {Note(SplitStartData)}/{Dyn(SplitStartData)}...{Note(SplitStopData)}/{Dyn(SplitStopData)} @ 0x{SoundPointer:x04}";
}

[StructLayout(LayoutKind.Sequential)]
readonly struct Sound
{
    public readonly UInt16 PtrtabPointer;

    public readonly UInt16 Status;

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

[StructLayout(LayoutKind.Sequential)]
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

    public override string ToString() =>
        $"ObjectCounts {bitfield:b016}: {CountMA1()} MA1, {CountMA2()} MA2, {CountMB()} MB, {CountMX()} MX, {CountMY()} MY";
}

[StructLayout(LayoutKind.Sequential)]
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

[StructLayout(LayoutKind.Sequential)]
readonly struct MB
{
    private readonly UInt16 field0;
    private readonly UInt16 field1;

    public override string ToString()
    {
        return $"{nameof(MB)} {field0:b016} {field1:b016}";
    }
}

[StructLayout(LayoutKind.Sequential)]
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

[StructLayout(LayoutKind.Sequential)]
readonly struct Modulator
{
    private readonly UInt16 field0;
    private readonly UInt16 field1;

    public override string ToString()
    {
        return $"{nameof(Modulator)} {field0:b016} {field1:b016}";
    }
}

record Envelope(UInt16 Header, Envelope.Segment[] Segments)
{
    public enum EnvelopeSegment
    {
        Continue = 0,
        Loop = 1,
        SustainPoint = 2,
        End = 3,
        EndAtSustainPoint = 4,
    }

    public record struct Segment(EnvelopeSegment Type, UInt16 Field0, UInt16 Field1);

    private const int MaxSegments = 8;

    private static IEnumerable<Segment> ReadSegments(BinaryReader input)
    {
        int i = 0;
        for (; i < MaxSegments; i++)
        {
            UInt16 segment = input.ReadUInt16();
            EnvelopeSegment type = (EnvelopeSegment)(segment >> 13);
            switch (type)
            {
                case EnvelopeSegment.Loop:
                    yield return new Segment(type, segment, input.ReadUInt16());
                    break;
                case EnvelopeSegment.Continue:
                case EnvelopeSegment.SustainPoint:
                    yield return new Segment(type, segment, default);
                    break;
                case EnvelopeSegment.End:
                case EnvelopeSegment.EndAtSustainPoint:
                    yield return new Segment(type, segment, default);
                    yield break;
                default:
                    throw new ArgumentOutOfRangeException($"Encountered code ${type} out of range");
            }
        }
        // I guess an envelope implicitly ends at 8 segments?
    }

    public static Envelope ReadDynamic(BinaryReader input)
    {
        UInt16 header = input.ReadUInt16();
        var segments = ReadSegments(input).ToArray();
        return new Envelope(header, segments);
    }

    public override string ToString() => $"{nameof(Envelope)} with {Segments.Length} segments";
}

[StructLayout(LayoutKind.Sequential)]
public struct PtrTab
{
    public readonly UInt16 Mix1Value;
    public readonly UInt16 Mix1Address;
    public readonly UInt16 Mix2Value;
    public readonly UInt16 Mix2Address;
    public readonly UInt16 Mix3Value;
    public readonly UInt16 Mix3Address;
    public readonly UInt16 Mix4Value;
    public readonly UInt16 Mix4Address;
    public readonly UInt16 _Unused1;
    public readonly UInt16 Mod0Pointer;
    public readonly UInt16 Mod1Pointer;
    public readonly UInt16 _Unused2;

    private int CountMixesUsed()
    {
        int i = 0;
        if (Mix1Value != 0) i++;
        if (Mix2Value != 0) i++;
        if (Mix3Value != 0) i++;
        if (Mix4Value != 0) i++;
        return i;
    }

    public override string ToString() => $"{nameof(PtrTab)} with {CountMixesUsed()} mixes";
}

[StructLayout(LayoutKind.Sequential, Size = 6)]
public struct RelocEntry
{
    public UInt32 Pointer;
    public PointerType Type;
    public byte Block;

    public enum PointerType : byte
    {
        Param = 0,
        MA2Page = 1,
        MA2Loop = 2,
        MBEnd = 3,
    }
}