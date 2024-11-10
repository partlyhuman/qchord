namespace Partlyhuman.Qchord;

internal static class Swizzler
{
    public static void Swizzle(SwizzleTracksOptions opts)
    {
        var from = opts.FromTracks.Select(x => x - 1).ToArray();
        var to = opts.ToTracks.Select(x => x - 1).ToArray();
        if (from.Length != to.Length)
        {
            throw new ArgumentException("Number of from tracks doesn't match number of to tracks");
        }

        (int from, int to)[] swizzles = from.Zip(to).ToArray();

        foreach (var s in swizzles)
        {
            Console.WriteLine($"{s.from} -> {s.to}");
        }
    }
}