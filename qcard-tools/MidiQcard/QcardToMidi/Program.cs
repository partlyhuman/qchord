using System.Diagnostics;
using QcardToMidi;

Debug.Assert(args.Length >= 3, "<path to qcard> <song number> <output file>");
string inputPath = args[0];
int trackNum = int.Parse(args[1]) - 1;
Debug.Assert(trackNum >= 0);
string outputPath = args[2];

QCard qCard = new(File.ReadAllBytes(inputPath));
Console.WriteLine(qCard);

if (File.Exists(outputPath)) File.Delete(outputPath);
using FileStream fileStream = File.Create(outputPath);
using BinaryWriter writer = new(fileStream);

switch (Path.GetExtension(outputPath).ToLower().TrimStart('.'))
{
    case "mid":
    case "midi":
        qCard.ConvertToMidiFile(writer, trackNum);
        Console.WriteLine($"Exported MIDI to {outputPath}");
        break;
    case "bin":
        qCard.ConvertToMidiStream(writer, trackNum, writeTimes: false, muteSpecials: true);
        Console.WriteLine($"Exported raw MIDI event data to {outputPath}");
        break;
    default:
        throw new ArgumentException("Unsupported output file type, supports .mid/.midi or .bin");
}

