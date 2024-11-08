using System.Text.RegularExpressions;

namespace Partlyhuman.Qchord;

internal record struct Chord(Chord.Root root, Chord.Quality quality)
{
    public enum Root
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

    public enum Quality
    {
        Maj = 1,
        Min = 2,
        Dim = 3,
        _7 = 4,
        Maj7 = 5,
        Min7 = 6,
        Aug = 7,
    }
}

/// <summary>
/// Utility to assist in manually adding chords to a QChord track, by trying to modify plain text guitar tabs,
/// replacing the guitar chord notation with the equivalent MIDI events.
/// This is by NO MEANS correct or exhaustive. It's a best-effort helper.
/// </summary>
internal static partial class Tabs
{
    private static Chord.Quality Combine(Chord.Quality a, Chord.Quality b) => (a, b) switch
    {
        (Chord.Quality.Maj, Chord.Quality._7) => Chord.Quality.Maj7,
        (Chord.Quality.Min, Chord.Quality._7) => Chord.Quality.Min7,
        _ => b,
    };

    private static Chord.Root? ParseRoot(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return null;
        }

        if (Enum.TryParse(str.Replace("#", "s"), ignoreCase: true, out Chord.Root root))
        {
            return root;
        }

        return null;
    }

    private static Chord.Quality ParseQuality(string str) => str switch
    {
        "m" or "min" => Chord.Quality.Min,
        "M" or "maj" or "Maj" => Chord.Quality.Maj,
        "aug" or "+" => Chord.Quality.Aug,
        "7" => Chord.Quality._7,
        _ => Chord.Quality.Maj,
    };

    // ReSharper disable once StringLiteralTypo
    [GeneratedRegex(@"\b(?<note>[ABCDEFG][#b]?)\s?(?<quality>m|M|min|Maj|maj|\+|aug|sus|7){0,2}\b")]
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
            MatchCollection matches = TabRegex().Matches(line);

            if (!matches.Any())
            {
                Console.Write(line);
            }
            else
            {
                // TODO reject lines that have stuff before matches.First().Index (treat as lyrics)
                // TODO modify to a replacer function and pass through whitespace

                foreach (Match match in matches)
                {
                    if (ParseRoot(match.Groups["note"].Value) is not { } root)
                    {
                        continue;
                    }

                    Chord.Quality[] qualities = match.Groups["quality"].Captures.Select(c => ParseQuality(c.Value)).ToArray();
                    Chord.Quality quality = qualities.Length switch
                    {
                        0 => Chord.Quality.Maj,
                        1 => qualities[0],
                        _ => qualities.Aggregate(Combine),
                    };

                    Console.Write($"{match.Value} => {root}{quality}  ");
                }
            }

            Console.WriteLine();
        }
    }
}