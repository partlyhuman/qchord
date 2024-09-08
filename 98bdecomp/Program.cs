using System.Runtime.InteropServices;
using _98bdecomp;

byte[] bytes = File.ReadAllBytes(args[0]);
// Stream stream = new MemoryStream(bytes);
Span<byte> buffer = bytes.AsSpan();

for (int offset = 0; offset < buffer.Length;)
{
    RiffChunk chunk = new RiffChunk(bytes, ref offset);
    Console.WriteLine($"Chunk {chunk.Header.Id} length {chunk.Data.Length}");
    switch (chunk.Header.Id)
    {
        case "PRIO":
        {
            var prio = MemoryMarshal.Read<PriorityData>(chunk.Data);
            Console.WriteLine($"Soundbank {prio.Name} priority {prio.Priority}");
            break;
        }

        case "IMAP":
        {
            int split1 = chunk.Data.IndexOf(stackalloc byte[] { 0xff, 0xff });
            int split2 = chunk.Data.LastIndexOf(stackalloc byte[] { 0xff, 0xff });

            Span<byte> instrumentTableSpan = chunk.Data[..split1];
            Span<byte> drumsetTableSpan = chunk.Data.Slice(split1 + 2, split2 - split1);

            Span<InstrumentTableData> instrumentTable = MemoryMarshal.Cast<byte, InstrumentTableData>(instrumentTableSpan);
            Span<InstrumentTableData> drumsetTable = MemoryMarshal.Cast<byte, InstrumentTableData>(drumsetTableSpan);

            Console.WriteLine($"Instrument table found at {offset + split1:x8} + {instrumentTableSpan.Length:x8}");
            Console.WriteLine($"with {instrumentTable.Length} instruments");
            Console.WriteLine($"Drumset table found at {offset + split2 + 2:x8} + {drumsetTableSpan.Length:x8}");
            Console.WriteLine($"with {drumsetTable.Length} instruments");
            break;
        }
        default:
            Console.WriteLine("Unimplemented, stopping");
            return;
    }
}