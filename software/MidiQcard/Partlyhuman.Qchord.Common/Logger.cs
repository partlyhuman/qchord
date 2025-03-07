using System.Diagnostics;

namespace Partlyhuman.Qchord.Common;

public static class Logger
{
    // private static Lazy<bool> IsDebug = new(() => Environment.GetEnvironmentVariable("DEBUG")?.ToLowerInvariant() is "1" or "true");

    [Conditional("DEBUG")]
    public static void Log(string x, bool newline = true)
    {
        Console.Error.Write(x);
        if (newline) Console.Error.WriteLine();
    }

    public static void WriteLogging(this BinaryWriter writer, byte b)
    {
        writer.Write(b);
        Log(b.ToString("X2"), false);
    }

    public static void WriteLogging(this BinaryWriter writer, ReadOnlySpan<byte> bb)
    {
        writer.Write(bb);
        Log(Convert.ToHexString(bb), false);
    }
}