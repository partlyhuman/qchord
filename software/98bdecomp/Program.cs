using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using _98bdecomp;

Debug.Assert(args.Length > 0, "First argument = path to 94b file");
string inputPath = args[0];
byte[] bytes = File.ReadAllBytes(inputPath);

Span<byte> buffer = bytes.AsSpan();

for (int offset = 0; offset < buffer.Length;)
{
    RiffChunk chunk = new RiffChunk(bytes, ref offset);
    Console.WriteLine($"> Chunk {chunk.Header.Id} length {chunk.Data.Length}");
    switch (chunk.Header.Id)
    {
        case "PRIO":
        {
            PriorityData prio = MemoryMarshal.Read<PriorityData>(chunk.Data);
            Console.WriteLine($"Soundbank {prio.Name} priority {prio.Priority}");
            break;
        }

        case "IMAP":
        {
            ReadOnlySpan<byte> marker = [0xff, 0xff];
            int split1 = chunk.Data.IndexOf(marker);
            int split2 = chunk.Data.LastIndexOf(marker);

            Span<byte> instrumentTableSpan = chunk.Data[..split1].Trim(marker);
            Span<byte> drumsetTableSpan = chunk.Data[split1..split2].Trim(marker);

            var instrumentTable = MemoryMarshal.Cast<byte, InstrumentTableData>(instrumentTableSpan);
            var drumsetTable = MemoryMarshal.Cast<byte, InstrumentTableData>(drumsetTableSpan);

            Console.WriteLine(
                $"Instrument table found at {offset + split1:x8} + {instrumentTableSpan.Length:x8} with {instrumentTable.Length} instruments");
            foreach (var instrument in instrumentTable)
            {
                Console.WriteLine(instrument);
            }

            Console.WriteLine($"Drumset table found at {offset + split2 + 2:x8} + {drumsetTableSpan.Length:x8} with {drumsetTable.Length} drumsets");
            break;
        }

        case "PARA":
        {
            // Easier to parse this as a stream where we can consume variable sizes
            using Stream chunkStream = new MemoryStream(bytes, offset - chunk.Data.Length, chunk.Data.Length);
            using BinaryReader reader = new BinaryReader(chunkStream);

            var globalBase = offset - chunk.Data.Length;
            // Just worry about one for now

            // 1. grab split definitions until the last one
            InstrumentSplitDefinition[] splits = InstrumentSplitDefinition.ReadAllSplits(reader).ToArray();

            for (int si = 0; si < splits.Length; si++)
            {
                InstrumentSplitDefinition splitDefinition = splits[si];
                // Sound pointer points to the PtrTab, the struct begins one word earlier
                reader.BaseStream.Seek((splitDefinition.SoundPointer - 1) * sizeof(UInt16), SeekOrigin.Begin);
                Console.WriteLine($"---Sound {si} at 0x{chunkStream.Position / 2:x04} in chunk");
                Sound sound = reader.ReadStruct<Sound>();
                Console.WriteLine(sound);

                Console.WriteLine($"{sound.CountKeyboardTables()} Kbd pointers:");
                for (int i = 0; i < sound.CountKeyboardTables(); i++)
                {
                    var ptr = reader.ReadUInt16();
                    Console.WriteLine($"{ptr:x04}");
                }

                Console.WriteLine($"{sound.CountEnvelopes()} Eg pointers:");
                for (int i = 0; i < sound.CountEnvelopes(); i++)
                {
                    var ptr = reader.ReadUInt16();
                    Console.WriteLine($"{ptr:x04}");
                }

                Console.WriteLine($"{sound.CountModulators()} Mod pointers:");
                for (int i = 0; i < sound.CountModulators(); i++)
                {
                    var ptr = reader.ReadUInt16();
                    Console.WriteLine($"{ptr:x04}");
                }

                // Should be at the object count object
                ObjectCounts oc = reader.ReadStruct<ObjectCounts>();
                Console.WriteLine(oc);

                // Ma1 objects definition
                for (int i = 0; i < oc.CountMA1(); i++)
                {
                    MA1 ma1 = reader.ReadStruct<MA1>();
                    Console.WriteLine(ma1);
                }

                // Ma2 objects definition
                for (int i = 0; i < oc.CountMA2(); i++)
                {
                    MA2 ma2 = MA2.ReadDynamic(reader);
                    Console.WriteLine(ma2);
                }

                // Mb objects definition
                for (int i = 0; i < oc.CountMB(); i++)
                {
                    MB mb = reader.ReadStruct<MB>();
                    Console.WriteLine(mb);
                }

                // Mx objects definition
                for (int i = 0; i < oc.CountMX(); i++)
                {
                    MX mx = reader.ReadStruct<MX>();
                    Console.WriteLine(mx);
                }

                // My objects definition
                for (int i = 0; i < oc.CountMY(); i++)
                {
                    MY my = MY.ReadDynamic(reader);
                    Console.WriteLine(my);
                }

                // Modulator definitions
                for (int i = 0; i < sound.CountModulators(); i++)
                {
                    var modulator = reader.ReadStruct<Modulator>();
                    Console.WriteLine(modulator);
                }

                // Enveloppe generator definitions
                for (int i = 0; i < sound.CountEnvelopes(); i++)
                {
                    var envelope = Envelope.ReadDynamic(reader);
                    Console.WriteLine(envelope);
                }

                // how far are we?
                // Span<byte> streamUntilHere = chunk.Data[..(int)chunkStream.Position];
                // // Dump the instrument splits to disk to compare to 94I, should appear as MINS block
                // string dumpPath = Path.Combine(Path.GetDirectoryName(inputPath), "temp.MINS.bin");
                // File.WriteAllBytes(dumpPath, streamUntilHere.ToArray());
                // Console.WriteLine($"Dumped to {dumpPath}");

                Console.WriteLine($"pointer = chunk words {chunkStream.Position / 2:x04} vs ptrtab pointer = {sound.PtrtabPointer:x04}");

                // Keyboard table definitions
                if (sound.CountKeyboardTables() is var keyboardTableCount and > 0)
                {
                    Console.WriteLine($"{keyboardTableCount} keyboard tables... not implemented/needed");
                }

                // Ptrpab table definitions
                Console.Write($"Pointer table @{sound.PtrtabPointer:x04}: ");
                PtrTab ptrtab = MemoryMarshal.Read<PtrTab>(chunk.Data[(sound.PtrtabPointer * sizeof(UInt16))..]);
                Console.WriteLine(ptrtab);
            }

            Debugger.Break();
            break;
        }
        case "RLOC":
        {
            var relocs = MemoryMarshal.Cast<byte, RelocEntry>(chunk.Data);
            Console.WriteLine("Reloc table: ");
            foreach (var grouping in relocs.ToArray().GroupBy(r => r.Type))
            {
                Console.WriteLine($" {grouping.Count()} entries of type {grouping.Key}");
            }

            break;
        }
        case "SAMP":
        {
            Console.WriteLine("Sample table");
            Console.WriteLine("TBD");
            break;
        }
        default:
            throw new ArgumentOutOfRangeException($"Unexpected chunk {chunk.Header.Id}");
            break;
    }
}

// Game plan:
// It's actually NOT THAT BAD, because of the RELOC TABLE!!! AHHHH!!!
// 0. plan to simply append:
//    new PARA = base PARA + donor PARA 
//    new SAMP = base SAMP + donor SAMP
// 1. go over donor RELOC, dereference the pointers, and add how much they're shifting up, like base PARA or base SAMP or page-based, depending on the type of pointer
// 2. shift the donor RELOC entries themselves (by base PARA, the pointers all live in PARA space)
//    in other words, RELOC is Reloc entries, with pointers to pointers in PARA, which point to various different things based on the reloc entry type.
//    the reloc entry pointers shift up base PARA because they all are in PARA space and we simply concatenated base PARA + donor PARA
// 3. base PARA + donor modified PARA = new PARA
//    base SAMP + donor SAMP = new SAMP
//    base RELOC + donor modified RELOC = new RELOC
// 4. Hack the base IMAP instrument table, for the replaced base programs, substitute the new PARA pointer (from donor instrument table),
//    remembering to shift up base PARA
// 5. profit???????
// Not included: samples or para crossing borders. may be able to get away without handling this while doing omnichord replacements because they're so small, we may luck out