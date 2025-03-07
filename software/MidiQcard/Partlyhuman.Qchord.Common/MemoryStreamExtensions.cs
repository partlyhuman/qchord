namespace Partlyhuman.Qchord.Common;

public static class MemoryStreamExtensions
{
    public static ReadOnlySpan<byte> AsSpan(this MemoryStream ms) =>
        ms.GetBuffer().AsSpan(0, (int)ms.Position);
}