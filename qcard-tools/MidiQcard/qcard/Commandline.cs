using System.Buffers.Binary;
using CommandLine;
using Partlyhuman.Qchord.Common;

namespace Partlyhuman.Qchord;

internal static class Commandline
{
    public static void Main(string[] args)
    {
        Parser parser = new(with =>
        {
            with.HelpWriter = Console.Out;
            with.AutoHelp = true;
            with.AutoVersion = false;
            with.AllowMultiInstance = true;
            with.CaseInsensitiveEnumValues = true;
        });
        parser.ParseArguments<ExtractOptions, BuildOptions, AddMetronomeOptions, ConvertTabsOptions, SwizzleTracksOptions>(args)
            .WithParsed<ExtractOptions>(Extract)
            .WithParsed<BuildOptions>(Build)
            .WithParsed<AddMetronomeOptions>(AddMetronome)
            .WithParsed<ConvertTabsOptions>(Tabs.ConvertTabs)
            .WithParsed<SwizzleTracksOptions>(Swizzler.Swizzle);
    }

    private static string GetExt(string path) => Path.GetExtension(path).Trim('.').ToLower();

    private static Format ParseFormat(string str) => str.ToLowerInvariant() switch
    {
        "mid" => Format.MID,
        "midi" => Format.MID,
        "bin" => Format.BIN,
        _ => throw new ArgumentException(
            "Cannot infer output format from filename, output either a midi or bin file, or choose a format explicitly"),
    };

    private static void Build(BuildOptions opts)
    {
        string[] inputPaths = opts.InputPaths.ToArray();
        string outputPath = opts.OutputPath;
        Format format = opts.Format ?? ParseFormat(GetExt(inputPaths[0]));

        var tracks = inputPaths.Select(path => format switch
        {
            Format.BIN => new QcardMidiTrack(File.ReadAllBytes(path)),
            Format.MID => new QcardMidiTrack(new MidiFileReader(File.ReadAllBytes(path))),
            _ => throw new ArgumentOutOfRangeException()
        });
        var qCard = new QCard(tracks.ToArray());

        using FileStream outputStream = File.Create(outputPath);
        outputStream.Write(qCard.AsSpan());

        Console.WriteLine(
            $"Assembled {qCard.TrackCount} raw track[s] into {qCard.AsSpan().Length >> 10}kb Qcard {Path.GetFileName(outputPath)}");
        Console.WriteLine(qCard);
    }

    private static void Extract(ExtractOptions opts)
    {
        QCard qCard = new(File.ReadAllBytes(opts.QcardPath));
        Console.WriteLine(qCard);

        if (opts.TrackNum is { } trackNum)
        {
            // Tracks are 1-indexed for humans, 0-indexed here
            ExtractOne(qCard, trackNum - 1, opts);
        }
        else
        {
            ExtractAll(qCard, opts);
        }
    }

    private static void ExtractOne(QCard qCard, int trackNum, ExtractOptions opts)
    {
        ArgumentException.ThrowIfNullOrEmpty(opts.OutputPath);
        if (trackNum < 0 || trackNum >= qCard.TrackCount)
        {
            throw new ArgumentOutOfRangeException(nameof(opts.TrackNum), $"QCard only has {qCard.TrackCount} tracks");
        }

        string outputPath = opts.OutputPath;
        Format format = opts.Format ?? ParseFormat(GetExt(outputPath));
        using FileStream fileStream = File.Create(outputPath);

        if (format is Format.MID)
        {
            using BinaryWriter writer = new(fileStream);
            qCard[trackNum].WriteMidiFile(writer);
            Console.WriteLine($"Exported Qcard[{trackNum}] MIDI to {outputPath}");
        }
        else if (format is Format.BIN)
        {
            fileStream.Write(qCard[trackNum].AsSpan());
            // qCard[trackNum].WriteMidiStream(writer, writeTimes: false, suppressSpecials: true);
            Console.WriteLine($"Exported QCard[{trackNum}] QChord data to {outputPath}");
        }
    }

    private static void ExtractAll(QCard qCard, ExtractOptions opts)
    {
        Format format = opts.Format ?? Format.MID;
        string dir = opts.OutputPath ?? ".";
        string basename = Path.GetFileNameWithoutExtension(opts.QcardPath);
        for (int i = 0; i < qCard.TrackCount; i++)
        {
            string outputPath = Path.Combine(dir, $"{basename}_track{i + 1:d2}.{format.ToString().ToLower()}");
            using FileStream fileStream = File.Create(outputPath);

            if (format is Format.MID)
            {
                using BinaryWriter writer = new(fileStream);
                qCard[i].WriteMidiFile(writer);
            }
            else if (format is Format.BIN)
            {
                fileStream.Write(qCard[i].AsSpan());
            }

            Console.WriteLine($"Exported {Path.GetFileName(outputPath)}");
        }
    }

    private static void AddMetronome(AddMetronomeOptions opts)
    {
        MidiFileReader midiReader = new(File.ReadAllBytes(opts.InputPath));
        long duration = midiReader.SumDuration();
        using MemoryStream secondTrackStream = new();
        using BinaryWriter secondTrackWriter = new(secondTrackStream);

        Span<byte> headerCopy = midiReader.GetHeaderData().ToArray();
        UInt16 midiTickDiv = BinaryPrimitives.ReadUInt16BigEndian(headerCopy[4..]);
        BinaryPrimitives.WriteUInt16BigEndian(headerCopy, 1); // mode 1 = multiple tracks

        Console.WriteLine($"Duration={duration} ticks, quarter note={midiTickDiv} ticks");

        for (long time = 0; time < duration; time += midiTickDiv)
        {
            secondTrackWriter.Write(MidiFileReader.WriteVariableLengthQuantity(midiTickDiv));
            secondTrackWriter.Write([0xB0, 0x2C, 0x7F]);
        }

        using BinaryWriter fileWriter = new(File.Create(opts.OutputPath));
        Chunk.WriteChunk(fileWriter, headerCopy, Chunk.MidiHeader);
        Chunk.WriteChunk(fileWriter, midiReader.GetTrackData(), Chunk.MidiTrack);
        Chunk.WriteChunk(fileWriter, secondTrackStream.AsSpan(), Chunk.MidiTrack);

        Console.WriteLine($"Wrote MIDI type 1 to {opts.OutputPath}. Please flatten to type 0 before converting to Qcard.");
    }
}