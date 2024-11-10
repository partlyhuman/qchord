// ReSharper disable ClassNeverInstantiated.Global

using CommandLine;
using CommandLine.Text;

namespace Partlyhuman.Qchord;

internal enum Format
{
    BIN,
    MIDI,
}

[Verb("extract", HelpText = "Extract one or all tracks from a QCard")]
internal class ExtractOptions
{
    [Value(0, MetaName = "input", Required = true, HelpText = "Path to a QCard file")]
    public string QcardPath { get; set; } = "";

    [Value(1, MetaName = "output", Required = false, HelpText = "Output filename if one track specified, or directory if all tracks")]
    public string? OutPath { get; set; }

    [Option('f', "format", Required = false, HelpText = "BIN: extracts QChord format track data without conversion. MIDI: converts to MIDI type 0.")]
    public Format? Format { get; set; }

    [Option('t', "track", Required = false, HelpText = "Which track number (starting with 1). Omit to export all tracks")]
    public int? TrackNum { get; set; }

    [Usage]
    public static IEnumerable<Example> Examples
    {
        get
        {
            yield return new Example("Extract and convert all tracks from a QCard dump to MIDI files in the current directory",
                new ExtractOptions { QcardPath = "qsc5.bin", Format = Qchord.Format.MIDI });
            yield return new Example("Extract all tracks from a QCard dump as raw Qchord track data in a specified directory",
                new ExtractOptions { QcardPath = "qsc5.bin", Format = Qchord.Format.BIN, OutPath = "out/" });
            yield return new Example(
                $"Convert track 1 from a QCard dump to MIDI. The --{nameof(Format).ToLower()} option is inferred by the output filename",
                new ExtractOptions { QcardPath = "qsc5.bin", TrackNum = 1, OutPath = "track1.mid" });
        }
    }
}

[Verb("build", HelpText = "Create a QCard from individual tracks")]
internal class BuildOptions
{
    [Value(0, MetaName = "output", Required = true, HelpText = "QCard file to assemble")]
    public string QcardPath { get; set; } = "";

    [Value(1, Min = 1, MetaName = "inputs", Required = true, HelpText = "One or more MIDI files OR Qchord track files to assemble")]
    public IEnumerable<string> InputPaths { get; set; } = [];

    [Option('f', "format", Required = false, HelpText = "BIN: assembles QCard out of Qchord track data. " +
                                                        "MIDI: converts MIDI type 0 files to tracks. " +
                                                        "If omitted, inferred from input file extensions")]
    public Format? Format { get; set; }

    [Usage]
    public static IEnumerable<Example> Examples
    {
        get
        {
            yield return new Example("Assemble a new QCard image from the given MIDI files",
                new BuildOptions { QcardPath = "qcard.bin", InputPaths = ["track1.mid", "track2.mid", "track3.mid"] });
            yield return new Example("Reassemble a QCard image from previously extracted raw Qchord tracks",
                new BuildOptions { QcardPath = "qcard.bin", InputPaths = ["track1.bin", "track2.bin", "track3.bin"] });
        }
    }
}

[Verb("metronome", HelpText = "Adds a QChord metronome track to a MIDI file. " +
                              "Currently this relies on an external tool like Sekaiju to merge the resulting multitrack type 1 MIDI " +
                              "into a single-track type 0 MIDI for further processing.")]
internal class AddMetronomeOptions
{
    [Value(0, MetaName = "input", Required = true, HelpText = "Path to a type 0 MIDI file")]
    public string MidiPath { get; set; } = "";
}

[Verb("tabs", HelpText = "Attempt to identify chords in plain text guitar tabs, and replace them with MIDI events in text")]
internal class ConvertTabsOptions
{
    [Value(0, MetaName = "input", Required = false, HelpText = "Tabs file, omit to accept console or pipe input")]
    public string? InputFile { get; set; }
}

[Verb("tracks", HelpText = "Batch move events between track numbers in a MIDI type 0 file. " +
                           "Helps make a MIDI file usable in QChord where some tracks are reserved.")]
internal class SwizzleTracksOptions
{
    [Value(0, MetaName = "input", Required = true, HelpText = "Path to a type 0 MIDI file")]
    public string InputPath { get; set; } = "";

    [Value(1, MetaName = "output", Required = true, HelpText = "A type 0 MIDI file to create")]
    public string OutputPath { get; set; } = "";

    [Option('i', "from", Required = true, Min = 1, HelpText = "From track, one-indexed, can repeat multiple")]
    public IEnumerable<int> FromTracks { get; set; } = [];

    [Option('o', "to", Required = true, Min = 1, HelpText = "To track, one-indexed, can repeat multiple")]
    public IEnumerable<int> ToTracks { get; set; } = [];
}