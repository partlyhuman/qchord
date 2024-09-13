using System.Diagnostics;
using QcardToMidi;

Debug.Assert(args.Length >= 2, "<path to qcard> <song number>");
string inputPath = args[0];
int songNumber = int.Parse(args[1]) - 1;
Debug.Assert(songNumber >= 0);

QCard qCard = new(File.ReadAllBytes(inputPath));
Console.WriteLine(qCard);

string outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, "out.bin");
if (File.Exists(outputPath)) File.Delete(outputPath);
using FileStream fs = File.Create(outputPath);
qCard.ConvertToMidiStreamNoTimes(songNumber, fs);

Console.WriteLine($"Wrote to {outputPath}");