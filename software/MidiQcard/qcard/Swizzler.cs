using Partlyhuman.Qchord.Common;

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

        if (from.Concat(to).Any(t => t is < -1 or > 15))
        {
            throw new ArgumentOutOfRangeException(nameof(opts), "Valid MIDI tracks are 1-16");
        }

        (int from, int to)[] swizzles = from.Zip(to).ToArray();

        // TODO assert type 0
        MidiFileReader midiReader = new(File.ReadAllBytes(opts.InputPath));
        ReadOnlySpan<byte> midiData = midiReader.GetTrackData();

        using MemoryStream trackStream = new();
        using BinaryWriter writer = new(trackStream);

        byte? status = null;
        while (midiData.Length > 0)
        {
            ReadOnlySpan<byte> eventBytes = MidiFileReader.ConsumeMidiEvent(ref midiData, ref status, out uint dt,
                out MidiStatus statusNibble, out ReadOnlySpan<byte> argumentBytes, out byte metaEventType);

            // Log("< " + Convert.ToHexString(eventBytes));
            // Log("> ", false);

            if (statusNibble is (MidiStatus.SystemExclusive or MidiStatus.NotAnEvent))
            {
                writer.Write(eventBytes);
                continue;
            }

            if (status == null) throw new InvalidOperationException("Status should be set");

            int channel = status.Value & 0xf;
            foreach ((int from, int to) swizzle in swizzles)
            {
                if (channel == swizzle.from)
                {
                    channel = swizzle.to;
                    break;
                }
            }

            if (channel >= 0)
            {
                byte modifiedStatus = (byte)(((int)statusNibble << 4) | channel);

                // var dtBytes = MidiFileReader.WriteVariableLengthQuantity(dt);
                // Span<byte> modifiedEvent = [..dtBytes, modifiedStatus, ..argumentBytes];
                // Log($"< {Convert.ToHexString(eventBytes)}\t> {Convert.ToHexString(modifiedEvent)}\tdt={dt}\tch={channel}\tst={statusNibble}");
                // writer.Write(modifiedEvent);

                writer.Write(MidiFileReader.WriteVariableLengthQuantity(dt));
                writer.Write(modifiedStatus);
                writer.Write(argumentBytes);
            }
        }

        string outputPath = opts.OutputPath;
        using BinaryWriter fileWriter = new BinaryWriter(File.Create(outputPath));

        Chunk.WriteChunk(fileWriter, midiReader.GetHeaderData(), Chunk.MidiHeader);
        Chunk.WriteChunk(fileWriter, trackStream.AsSpan(), Chunk.MidiTrack);

        Console.WriteLine($"Wrote MIDI type 0 to {outputPath}");
    }
}