using System.Diagnostics;
using QcardToMidi;

Debug.Assert(args.Length > 0, "First argument = path to qcard ROM");
string inputPath = args[0];
byte[] bytes = File.ReadAllBytes(inputPath);

var qCard = new QCard(bytes);

Console.WriteLine(qCard);

string outputPath = Path.Combine(Path.GetDirectoryName(inputPath), "out.bin");
if (File.Exists(outputPath)) File.Delete(outputPath);

using FileStream fs = File.Create(outputPath);
qCard.ConvertToMidiStreamNoTimes(1, fs);

Console.WriteLine($"Wrote to {outputPath}");