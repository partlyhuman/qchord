using Partlyhuman.Qchord.Common;

if (args.Length < 2)
{
    Console.WriteLine("<infile>+ <outfile.bin>\n" +
                      " One or more input files, should be either .bin which will be interpreted as the postprocessed track data,\n" +
                      " or .mid/.midi which will be converted to Qchord format. Do not mix formats.\n" +
                      " Output file will be a Qcard ROM if conversion succeeded.");
    return;
}

string GetExt(string path) => Path.GetExtension(path).Trim('.').ToLower();

string outputPath = args[^1];
if (GetExt(outputPath) != "bin")
{
    Console.WriteLine("Expect an output file to end in .bin");
    return;
}

string[] inputPaths = args[..^1];
var extensions = inputPaths.Select(GetExt).Distinct().ToArray();
if (extensions.Except(["bin"]).Any() == extensions.Except(["mid", "midi"]).Any())
{
    Console.WriteLine("Expect input files to all have the same extension, .bin, or .mid/.midi");
    return;
}

if (File.Exists(outputPath)) File.Delete(outputPath);
using FileStream outputStream = File.Create(outputPath);

switch (extensions[0])
{
    case "bin":
    {
        QchordMidiTrack[] tracks = inputPaths
            .Select(path => new QchordMidiTrack(File.ReadAllBytes(path)))
            .ToArray();
        var qCard = new QCard(tracks);
        outputStream.Write(qCard.Bytes);
        Console.WriteLine(qCard);
        Console.WriteLine($"Assembled {tracks.Length} raw track[s] into {qCard.Bytes.Length / 1024}kb Qcard {Path.GetFileName(outputPath)}");
        break;
    }
    case "mid":
    case "midi":
    {
        QchordMidiTrack[] tracks = inputPaths
            .Select(midiPath =>
            {
                MidiReader reader = new MidiReader(File.ReadAllBytes(midiPath));
                return QchordMidiTrack.FromMidi(reader);
            })
            .ToArray();
        var qCard = new QCard(tracks);
        outputStream.Write(qCard.Bytes);
        Console.WriteLine(qCard);
        Console.WriteLine($"Converted {tracks.Length} midi files[s] into {qCard.Bytes.Length / 1024}kb Qcard {Path.GetFileName(outputPath)}");
        break;
    }
}