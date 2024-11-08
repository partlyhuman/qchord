using System.Text.RegularExpressions;

namespace Partlyhuman.Qchord;

internal record struct Chord(ChordRoot Root, ChordQuality Quality);

/// Values correspond to the midi event's upper nibble
/// (<a href="https://github.com/partlyhuman/qchord/blob/master/qcards/README.md#chords">docs</a>)
internal enum ChordQuality : byte
{
    Maj = 1 << 4,
    Min = 2 << 4,
    Dim = 3 << 4,
    _7 = 4 << 4,
    Maj7 = 5 << 4,
    Min7 = 6 << 4,
    Aug = 7 << 4,
}

/// Values correspond to the midi event's lower nibble
/// (<a href="https://github.com/partlyhuman/qchord/blob/master/qcards/README.md#chords">docs</a>)
internal enum ChordRoot : byte
{
    C = 0,
    Cs = 1,
    Db = 1,
    D = 2,
    Ds = 3,
    Eb = 3,
    E = 4,
    F = 5,
    Fs = 6,
    Gb = 6,
    G = 7,
    Gs = 8,
    Ab = 8,
    A = 9,
    As = 10,
    Bb = 10,
    B = 11,
}

/// <summary>
/// Utility to assist in manually adding chords to a QChord track, by trying to modify plain text guitar tabs,
/// replacing the guitar chord notation with the equivalent MIDI events.
/// This is by NO MEANS correct or exhaustive. It's a best-effort helper.
/// </summary>
internal static partial class Tabs
{
    private static ChordQuality Combine(ChordQuality a, ChordQuality b) => (a, b) switch
    {
        (ChordQuality.Maj, ChordQuality._7) => ChordQuality.Maj7,
        (ChordQuality.Min, ChordQuality._7) => ChordQuality.Min7,
        _ => b,
    };

    private static ChordRoot? ParseRoot(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return null;
        }

        if (Enum.TryParse(str.Replace("#", "s").Replace("\u266f", "s").Replace("\u266d", "b"), /*ignoreCase: true,*/ out ChordRoot root))
        {
            return root;
        }

        return null;
    }

    private static ChordQuality ParseQuality(string str) => str switch
    {
        "m" or "min" or "-" => ChordQuality.Min,
        "M" or "maj" or "Maj" => ChordQuality.Maj,
        "aug" or "+" => ChordQuality.Aug,
        "7" or "7th" => ChordQuality._7,
        _ => ChordQuality.Maj,
    };

    // ReSharper disable once StringLiteralTypo
    [GeneratedRegex(@"\b(?<note>[ABCDEFG][#♯b♭]?)\s?(?<quality>m|M|min|Maj|maj|\+|\-|aug|sus|7|7th){0,2}\b")]
    private static partial Regex TabRegex();

    public static void ConvertTabs(ConvertTabsOptions opts)
    {
        StreamReader lines = opts.InputFile switch
        {
            _ when Console.IsInputRedirected => new StreamReader(Console.OpenStandardInput()),
            { Length: > 0 } inputFile => new StreamReader(inputFile),
            _ => throw new ArgumentNullException(nameof(opts.InputFile), "Provide a text file or pipe one to standard in"),
        };

        while (lines.ReadLine() is { } line)
        {
            // Do a little extra work to only process lines that begin with a match - e.g. don't try to match inside lyrics (like the word "am")
            Match firstMatch = TabRegex().Match(line);
            if (firstMatch.Success && string.IsNullOrWhiteSpace(line[..firstMatch.Index]))
            {
                Console.WriteLine(ParseLine(line));
            }
            else
            {
                Console.WriteLine(line);
            }
        }
    }

    private static string ParseLine(string line) => TabRegex().Replace(line, match =>
    {
        if (ParseRoot(match.Groups["note"].Value) is not { } root)
        {
            return match.Value;
        }

        ChordQuality[] qualities = match.Groups["quality"].Captures.Select(c => ParseQuality(c.Value)).ToArray();
        ChordQuality quality = qualities.Length switch
        {
            0 => ChordQuality.Maj,
            1 => qualities[0],
            _ => qualities.Aggregate(Combine),
        };

        Span<byte> midiEvent = [0xAA, (byte)((int)root | (int)quality), 0];

        // Console.Write($"{match.Value} => {root}{quality} => {Convert.ToHexString(midiEvent)}  "); // DEBUG
        return Convert.ToHexString(midiEvent);
    });
}