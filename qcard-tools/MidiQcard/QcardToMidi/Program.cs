using System.Diagnostics;
using QcardToMidi;

Debug.Assert(args.Length >= 2, "<path to qcard> <song number> [verify]");
string inputPath = args[0];
int songNumber = int.Parse(args[1]) - 1;
Debug.Assert(songNumber >= 0);
byte[] bytes = File.ReadAllBytes(inputPath);

var qCard = new QCard(bytes);
Console.WriteLine(qCard);

string outputPath = Path.Combine(Path.GetDirectoryName(inputPath), "out.bin");
if (File.Exists(outputPath)) File.Delete(outputPath);
using FileStream fs = File.Create(outputPath);

BinaryReader verifyReader = null;
if (args.Length > 2)
{
    verifyReader = new BinaryReader(File.OpenRead(args[2]));
}

qCard.ConvertToMidiStreamNoTimes(songNumber, fs, verifyReader);

verifyReader?.Dispose();
Console.WriteLine($"Wrote to {outputPath}");
