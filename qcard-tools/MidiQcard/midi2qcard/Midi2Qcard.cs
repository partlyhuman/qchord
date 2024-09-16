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
        QcardMidiTrack[] tracks = inputPaths
            .Select(path => new QcardMidiTrack(File.ReadAllBytes(path)))
            .ToArray();
        var qCard = new QCard(tracks);
        outputStream.Write(qCard.Bytes);
        Console.WriteLine(qCard);
        Console.WriteLine($"Assembled {tracks.Length} raw track[s] into {qCard.Bytes.Length >> 10}kb Qcard {Path.GetFileName(outputPath)}");
        break;
    }
    case "mid":
    case "midi":
    {
        // // DEBUG: dump a single track
        // var track = new QcardMidiTrack(new MidiFileReader(File.ReadAllBytes(inputPaths[0])));
        // outputStream.Write(track.AsSpan());
        // Console.WriteLine($"Wrote single track data to {outputPath}");

        QcardMidiTrack[] tracks = inputPaths
            .Select(midiPath => new QcardMidiTrack(new MidiFileReader(File.ReadAllBytes(midiPath))))
            .ToArray();
        var qCard = new QCard(tracks);
        outputStream.Write(qCard.Bytes);
        Console.WriteLine(qCard);
        Console.WriteLine($"Converted {tracks.Length} midi files[s] into {qCard.Bytes.Length >> 10}kb Qcard {Path.GetFileName(outputPath)}");
        break;
    }
}