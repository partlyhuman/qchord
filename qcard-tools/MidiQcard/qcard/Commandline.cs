using CommandLine;
using Partlyhuman.Qchord.Common;

enum Format
{
    BIN,
    MIDI,
}

[Verb("extract", HelpText = "Extract one or all tracks from a QCard")]
class ExtractOptions
{
    [Value(0, MetaName = "input", Required = true, HelpText = "Path to a QCard file")]
    public string QcardPath { get; set; }

    [Value(1, MetaName = "output", Required = false, HelpText = "Output filename if one track specified, or directory if all tracks")]
    public string? OutPath { get; set; }

    [Option('f', "format", Required = false, HelpText = "BIN: extracts QChord format track data without conversion. MIDI: converts to MIDI type 0.")]
    public Format? Format { get; set; }

    [Option('t', "track", Required = false, HelpText = "Which track number (starting with 1). Omit to export all tracks")]
    public int? TrackNum { get; set; }
}


[Verb("build", HelpText = "Create a QCard from MIDI tracks")]
class BuildOptions
{
}

internal static class Commandline
{
    public static void Main(string[] args)
    {
        Parser parser = new(with =>
        {
            with.HelpWriter = Console.Out;
            with.AutoHelp = true;
            with.CaseInsensitiveEnumValues = true;
        });
        parser.ParseArguments<ExtractOptions, BuildOptions>(args)
            .WithParsed<ExtractOptions>(Extract);
    }

    private static void Extract(ExtractOptions opts)
    {
        QCard qCard = new(File.ReadAllBytes(opts.QcardPath));
        Console.WriteLine(qCard);

        if (opts.TrackNum is { } trackNum)
        {
            ExtractOne(qCard, trackNum, opts);
        }
        else
        {
            ExtractAll(qCard, opts);
        }
    }

    private static void ExtractOne(QCard qCard, int trackNum, ExtractOptions opts)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(trackNum);
        if (trackNum > qCard.TrackCount)
        {
            throw new ArgumentOutOfRangeException(nameof(opts.TrackNum), $"QCard only has {qCard.TrackCount} tracks");
        }

        if (opts.OutPath is null)
        {
            throw new ArgumentException("We need an output file name if you are extracting a single track", nameof(opts.OutPath));
        }

        ArgumentException.ThrowIfNullOrEmpty(opts.OutPath);
        Format format = opts.Format ?? Path.GetExtension(opts.OutPath).TrimStart('.').ToLowerInvariant() switch
        {
            "mid" => Format.MIDI,
            "midi" => Format.MIDI,
            "bin" => Format.BIN,
            _ => throw new ArgumentException(
                "Cannot infer output format from filename, output either a midi or bin file, or choose a format explicitly"),
        };

        string outputPath = opts.OutPath;

        if (File.Exists(outputPath)) File.Delete(outputPath);
        using FileStream fileStream = File.Create(outputPath);
        using BinaryWriter writer = new(fileStream);

        switch (format)
        {
            case Format.MIDI:
                qCard[trackNum - 1].WriteMidiFile(writer);
                Console.WriteLine($"Exported track {trackNum} MIDI to {outputPath}");
                break;
            case Format.BIN:
                qCard[trackNum - 1].WriteMidiStream(writer, writeTimes: false, suppressSpecials: true);
                Console.WriteLine($"Exported track {trackNum} raw MIDI event data to {outputPath}");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void ExtractAll(QCard qCard, ExtractOptions opts)
    {
        Format format = opts.Format ?? Format.MIDI;
        string dir = opts.OutPath ?? ".";
        string basename = Path.GetFileNameWithoutExtension(opts.QcardPath);
        for (int i = 0; i < qCard.TrackCount; i++)
        {
            switch (format)
            {
                case Format.MIDI:
                {
                    string outputPath = Path.Combine(dir, $"{basename}_track{i + 1:d2}.mid");
                    using FileStream fileStream = File.Create(outputPath);
                    using BinaryWriter writer = new(fileStream);
                    qCard[i].WriteMidiFile(writer);
                    Console.WriteLine($"Exported {Path.GetFileName(outputPath)}");
                    break;
                }

                case Format.BIN:
                {
                    string outputPath = Path.Combine(dir, $"{basename}_track{i + 1:d2}.bin");
                    File.WriteAllBytes(outputPath, qCard[i].AsSpan().ToArray());
                    Console.WriteLine($"Exported {Path.GetFileName(outputPath)}");
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException();
            }
            // Temporary - export all tracks raw, could be used for recombination, tighter packing
        }
    }
}