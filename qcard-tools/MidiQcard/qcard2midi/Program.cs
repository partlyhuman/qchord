using System.Diagnostics;
using Partlyhuman.Qchord.Common;

if (args.Length < 1)
{
    Console.Out.WriteLine("<path to qcard> [song number] [output file]\n" +
                          " Output file can be .bin for raw midi events, .mid/.midi for a midi file\n" +
                          " Omit output file and all tracks will be generated.");
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
            qCard.TrackDataToMidiFile(writer, trackNum);
            Console.WriteLine($"Exported MIDI to {outputPath}");
            break;
        case "bin":
            qCard.TrackDataToMidiStream(writer, trackNum, writeTimes: false, suppressSpecials: true);
            Console.WriteLine($"Exported raw MIDI event data to {outputPath}");
            break;
        default:
            throw new ArgumentException("Unsupported output file type, supports .mid/.midi or .bin");
    }

    return;
}

if (args.Length >= 1)
{
    string dir = Path.GetDirectoryName(inputPath);
    string basename = Path.GetFileNameWithoutExtension(inputPath);
    for (int i = 0; i < qCard.TrackCount; i++)
    {
        string outputPath = Path.Combine(dir, $"{basename}_track{i+1:d2}.mid");
        using FileStream fileStream = File.Create(outputPath);
        using BinaryWriter writer = new(fileStream);
        qCard.TrackDataToMidiFile(writer, i);
        Console.WriteLine($"Exported {Path.GetFileName(outputPath)}");
    }
}