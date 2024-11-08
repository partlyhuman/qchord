// ReSharper disable ClassNeverInstantiated.Global

using CommandLine;

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