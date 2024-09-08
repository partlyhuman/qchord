using System.Diagnostics;
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
            // Span<byte> drumsetTableSpan = chunk.Data.Slice(split1 + marker.Length, split2 - split1 - marker.Length);

            Span<InstrumentTableData> instrumentTable = MemoryMarshal.Cast<byte, InstrumentTableData>(instrumentTableSpan);
            Span<InstrumentTableData> drumsetTable = MemoryMarshal.Cast<byte, InstrumentTableData>(drumsetTableSpan);

            Console.WriteLine(
                $"Instrument table found at {offset + split1:x8} + {instrumentTableSpan.Length:x8} with {instrumentTable.Length} instruments");
            Console.WriteLine($"Drumset table found at {offset + split2 + 2:x8} + {drumsetTableSpan.Length:x8} with {drumsetTable.Length} drumsets");
            break;
        }

        case "PARA":
        {
            
            Span<byte> data = chunk.Data;
            ReadOnlySpan<InstrumentSplitDefinition> splits = InstrumentSplitDefinition.TakeN(ref data);
            foreach (InstrumentSplitDefinition split in splits) Console.WriteLine(split);

            // // Dump the instrument splits to disk to compare to 94I, should appear as MINS block
            // string dumpPath = Path.Combine(Path.GetDirectoryName(inputPath), "temp.MINS.bin");
            // File.WriteAllBytes(dumpPath, MemoryMarshal.Cast<InstrumentSplitDefinition, byte>(splits).ToArray());
            // Console.WriteLine($"Dumped to {dumpPath}");

            var parsedSplitDefs = splits.ToArray().Select(x => x.Parse());

            // TODO associate ordering?

            Debugger.Break();
            break;
        }
        default:
            Console.WriteLine($"Unimplemented {chunk.Header.Id}, skipping");
            break;
    }
}