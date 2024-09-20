using System.Diagnostics;
using Partlyhuman.Qchord.Common;

if (args.Length < 1)
{
    Console.Out.WriteLine($"USAGE: {Path.GetFileName(Environment.ProcessPath)} <path to qcard> [song number] [output file]\n" +
                          "  Output file can be .bin for a single Qcard track data, .mid/.midi for a playable type 0 midi file.\n" +
                          "  Omit song number and output file, and all tracks will be extracted from Qcard.");
    return;
}

string inputPath = args[0];
QCard qCard = new(File.ReadAllBytes(inputPath));
Console.WriteLine(qCard);

if (args.Length >= 3)
{
    int trackNum = int.Parse(args[1]) - 1;
    Debug.Assert(trackNum >= 0);
    string outputPath = args[2];


    if (File.Exists(outputPath)) File.Delete(outputPath);
    using FileStream fileStream = File.Create(outputPath);
    using BinaryWriter writer = new(fileStream);

    switch (Path.GetExtension(outputPath).ToLower().TrimStart('.'))
    {
        case "mid":
        case "midi":
            qCard[trackNum].WriteMidiFile(writer);
            Console.WriteLine($"Exported track {trackNum} MIDI to {outputPath}");
            break;
        case "bin":
            qCard[trackNum].WriteMidiStream(writer, writeTimes: false, suppressSpecials: true);
            Console.WriteLine($"Exported track {trackNum} raw MIDI event data to {outputPath}");
            break;
        default:
            throw new ArgumentException("Unsupported output file type, supports .mid/.midi or .bin");
    }

    return;
}

if (args.Length >= 1)
{
    string dir = Path.GetDirectoryName(inputPath)!;
    string basename = Path.GetFileNameWithoutExtension(inputPath);
    for (int i = 0; i < qCard.TrackCount; i++)
    {
        // Temporary - export all tracks raw, could be used for recombination, tighter packing
        // string outputPath = Path.Combine(dir, $"{basename}_track{i + 1:d2}.bin");
        // File.WriteAllBytes(outputPath, qCard[i].AsSpan().ToArray());
        // Console.WriteLine($"Exported {Path.GetFileName(outputPath)}");

        string outputPath = Path.Combine(dir, $"{basename}_track{i + 1:d2}.mid");
        using FileStream fileStream = File.Create(outputPath);
        using BinaryWriter writer = new(fileStream);
        qCard[i].WriteMidiFile(writer);
        Console.WriteLine($"Exported {Path.GetFileName(outputPath)}");
    }
}