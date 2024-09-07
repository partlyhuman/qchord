// See https://aka.ms/new-console-template for more information

using _98bdecomp;



FileStream stream = File.OpenRead(args[0]);

// while (stream.Position != stream.Length)
// {
    RiffHeader header = Utils.ReadStruct<RiffHeader>(stream);
    Console.WriteLine($"Chunk {header.Id} length {header.Length}");
    switch (header.Id)
    {
        case "PRIO":
            var prio = Utils.ReadStruct<PrioChunk>(stream);
            Console.WriteLine($"Soundbank {prio.Name} priority {prio.Priority}");
            break;
        
        case "IMAP":
            
            break;
    }
// }
