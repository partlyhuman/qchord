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

            Span<InstrumentTableData> instrumentTable = MemoryMarshal.Cast<byte, InstrumentTableData>(instrumentTableSpan);
            Span<InstrumentTableData> drumsetTable = MemoryMarshal.Cast<byte, InstrumentTableData>(drumsetTableSpan);

            Console.WriteLine(
                $"Instrument table found at {offset + split1:x8} + {instrumentTableSpan.Length:x8} with {instrumentTable.Length} instruments");
            Console.WriteLine($"Drumset table found at {offset + split2 + 2:x8} + {drumsetTableSpan.Length:x8} with {drumsetTable.Length} drumsets");
            break;
        }

        case "PARA":
        {
            // Easier to parse this as a stream where we can consume variable sizes
            using Stream chunkStream = new MemoryStream(bytes, offset - chunk.Data.Length, chunk.Data.Length);
            using BinaryReader reader = new BinaryReader(chunkStream);

            // Just worry about one for now
            
            // 1. grab split definitions until the last one
            while (true)
            {
                var splitDef = reader.ReadStruct<InstrumentSplitDefinition>();
                Console.WriteLine(splitDef);
                if (splitDef.IsEnd()) break;
            }

            Split1 split = reader.ReadStruct<Split1>();
            Console.WriteLine(split);
            
            Console.WriteLine($"{split.CountKeyboardTables()} Kbd pointers:");
            for (int i = 0; i < split.CountKeyboardTables(); i++)
            {
                var ptr = reader.ReadUInt16();
                Console.WriteLine($"{ptr:x04}");
            }
            Console.WriteLine($"{split.CountEnvelopes()} Eg pointers:");
            for (int i = 0; i < split.CountEnvelopes(); i++)
            {
                var ptr = reader.ReadUInt16();
                Console.WriteLine($"{ptr:x04}");
            }
            Console.WriteLine($"{split.CountModulators()} Mod pointers:");
            for (int i = 0; i < split.CountModulators(); i++)
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
            for (int i = 0; i < split.CountModulators(); i++)
            {
                var modulator = reader.ReadStruct<Modulator>();
                Console.WriteLine(modulator);
            }
            
            // Enveloppe generator definitions
            for (int i = 0; i < split.CountEnvelopes(); i++)
            {
                var envelope = Envelope.ReadDynamic(reader);
                Console.WriteLine(envelope);
            }
            
            // how far are we?
            Span<byte> streamUntilHere = chunk.Data[..(int)chunkStream.Position];
            // Dump the instrument splits to disk to compare to 94I, should appear as MINS block
            string dumpPath = Path.Combine(Path.GetDirectoryName(inputPath), "temp.MINS.bin");
            File.WriteAllBytes(dumpPath, streamUntilHere.ToArray());
            Console.WriteLine($"Dumped to {dumpPath}");
            
            
            // Keyboard table definitions
            // Ptrpab table definitions
            
            
            

            Debugger.Break();
            break;
        }
        default:
            Console.WriteLine($"Unimplemented {chunk.Header.Id}, skipping");
            break;
    }
}