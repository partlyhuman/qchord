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
            with.CaseInsensitiveEnumValues = true;
        });
        parser.ParseArguments<ExtractOptions, BuildOptions, AddMetronomeOptions, ConvertTabsOptions>(args)
            .WithParsed<ExtractOptions>(Extract)
            .WithParsed<BuildOptions>(Build)
            .WithParsed<AddMetronomeOptions>(AddMetronome)
            .WithParsed<ConvertTabsOptions>(Tabs.ConvertTabs);
    }

    private static void AddMetronome(AddMetronomeOptions obj)
    {
        string inputPath = obj.MidiPath;
        MidiFileReader midiReader = new(File.ReadAllBytes(inputPath));
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

        string outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, $"{Path.GetFileNameWithoutExtension(inputPath)}_metronome.mid");
        using BinaryWriter fileWriter = new BinaryWriter(File.Create(outputPath));

        Chunk.WriteChunk(fileWriter, headerCopy, Chunk.MidiHeader);
        Chunk.WriteChunk(fileWriter, midiReader.GetTrackData(), Chunk.MidiTrack);
        Chunk.WriteChunk(fileWriter, secondTrackStream.AsSpan(), Chunk.MidiTrack);

        Console.WriteLine($"Wrote MIDI type 1 to {outputPath}");
    }

    private static void Build(BuildOptions opts)
    {
        string GetExt(string path) => Path.GetExtension(path).Trim('.').ToLower();

        string[] inputPaths = opts.InputPaths.ToArray();
        string outputPath = opts.QcardPath;

        if (GetExt(outputPath) != "bin")
        {
            throw new ArgumentException("Expect an output file to end in .bin");
        }

        if (File.Exists(outputPath)) File.Delete(outputPath);
        using FileStream outputStream = File.Create(outputPath);

        Format format = opts.Format ?? Enum.Parse<Format>(GetExt(inputPaths[0]).Replace("mid", "midi"), ignoreCase: true);

        switch (format)
        {
            case Format.BIN:
            {
                QcardMidiTrack[] tracks = inputPaths
                    .Select(path => new QcardMidiTrack(File.ReadAllBytes(path)))
                    .ToArray();
                var qCard = new QCard(tracks);
                outputStream.Write(qCard.AsSpan());
                Console.WriteLine(
                    $"Assembled {tracks.Length} raw track[s] into {qCard.AsSpan().Length >> 10}kb Qcard {Path.GetFileName(outputPath)}");
                Console.WriteLine(qCard);
                break;
            }
            case Format.MIDI:
            {
                // // DEBUG: dump a single track
                // var track = new QcardMidiTrack(new MidiFileReader(File.ReadAllBytes(inputPaths[0])));
                // outputStream.Write(track.AsSpan());
                // Console.WriteLine($"Wrote single track data to {outputPath}");

                QcardMidiTrack[] tracks = inputPaths
                    .Select(midiPath => new QcardMidiTrack(new MidiFileReader(File.ReadAllBytes(midiPath))))
                    .ToArray();
                var qCard = new QCard(tracks);
                outputStream.Write(qCard.AsSpan());
                Console.WriteLine(
                    $"Converted {tracks.Length} midi files[s] into {qCard.AsSpan().Length >> 10}kb Qcard {Path.GetFileName(outputPath)}");
                Console.WriteLine(qCard);
                break;
            }
        }
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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(trackNum);
        if (trackNum >= qCard.TrackCount)
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
                qCard[trackNum].WriteMidiFile(writer);
                Console.WriteLine($"Exported Qcard[{trackNum}] MIDI to {outputPath}");
                break;
            case Format.BIN:
                qCard[trackNum].WriteMidiStream(writer, writeTimes: false, suppressSpecials: true);
                Console.WriteLine($"Exported QCard[{trackNum}] QChord data to {outputPath}");
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
        }
    }
}