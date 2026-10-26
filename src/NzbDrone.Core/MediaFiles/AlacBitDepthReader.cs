using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NzbDrone.Core.MediaFiles
{
    internal static class AlacBitDepthReader
    {
        public static AlacConfiguration? Read(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var moov = FindBox(stream, 0, stream.Length, "moov");
                if (!moov.HasValue)
                {
                    return null;
                }

                foreach (var track in ReadBoxes(stream, moov.Value.PayloadStart, moov.Value.End))
                {
                    if (track.Type != "trak")
                    {
                        continue;
                    }

                    var media = FindChild(stream, track, "mdia");
                    var mediaHeader = media.HasValue ? FindChild(stream, media.Value, "mdhd") : null;
                    var information = media.HasValue ? FindChild(stream, media.Value, "minf") : null;
                    var table = information.HasValue ? FindChild(stream, information.Value, "stbl") : null;
                    var descriptions = table.HasValue ? FindChild(stream, table.Value, "stsd") : null;

                    if (!mediaHeader.HasValue || mediaHeader.Value.End - mediaHeader.Value.PayloadStart < 4 || ReadByte(stream, mediaHeader.Value.PayloadStart) > 1 ||
                        !descriptions.HasValue || descriptions.Value.End - descriptions.Value.PayloadStart < 8)
                    {
                        continue;
                    }

                    // stsd has a four-byte full-box header and a four-byte entry count.
                    var entriesStart = descriptions.Value.PayloadStart + 8;
                    foreach (var entry in ReadBoxes(stream, entriesStart, descriptions.Value.End))
                    {
                        if ((entry.Type != "alac" && entry.Type != "mp4a") || entry.End - entry.PayloadStart < 28)
                        {
                            continue;
                        }

                        var sampleEntryVersion = ReadUInt16(stream, entry.PayloadStart + 8);
                        var extensionSize = sampleEntryVersion == 1 ? 16 : sampleEntryVersion == 2 ? 36 : 0;
                        if (sampleEntryVersion > 2)
                        {
                            continue;
                        }

                        var childBoxesStart = entry.PayloadStart + 28 + extensionSize;
                        if (childBoxesStart > entry.End)
                        {
                            continue;
                        }

                        foreach (var child in ReadBoxes(stream, childBoxesStart, entry.End))
                        {
                            if (child.Type != "alac" || child.End - child.PayloadStart < 28)
                            {
                                continue;
                            }

                            // The ALAC box contains full-box version/flags followed by the
                            // 24-byte ALACSpecificConfig. Its bitDepth is byte five of that config.
                            if (ReadUInt32(stream, child.PayloadStart) != 0 || ReadByte(stream, child.PayloadStart + 8) != 0)
                            {
                                continue;
                            }

                            var bitDepth = ReadByte(stream, child.PayloadStart + 9);
                            if (bitDepth >= 16 && bitDepth <= 32)
                            {
                                return new AlacConfiguration(bitDepth, ReadUInt32(stream, child.PayloadStart + 20), ReadUInt32(stream, child.PayloadStart + 24));
                            }

                            return null;
                        }
                    }
                }

                return null;
            }
        }

        private static IsoBox? FindBox(Stream stream, long start, long end, string type)
        {
            foreach (var box in ReadBoxes(stream, start, end))
            {
                if (box.Type == type)
                {
                    return box;
                }
            }

            return null;
        }

        private static IsoBox? FindChild(Stream stream, IsoBox parent, string type)
        {
            return FindBox(stream, parent.PayloadStart, parent.End, type);
        }

        private static IEnumerable<IsoBox> ReadBoxes(Stream stream, long start, long end)
        {
            var position = start;
            while (position >= 0 && position <= end - 8)
            {
                var size = ReadUInt32(stream, position);
                var typeBytes = ReadBytes(stream, position + 4, 4);
                if (typeBytes == null)
                {
                    yield break;
                }

                var headerSize = 8L;
                ulong boxSize = size;
                if (size == 1)
                {
                    if (end - position < 16)
                    {
                        yield break;
                    }

                    var extendedSize = ReadUInt64(stream, position + 8);
                    if (extendedSize > long.MaxValue)
                    {
                        yield break;
                    }

                    boxSize = extendedSize;
                    headerSize = 16;
                }
                else if (size == 0)
                {
                    boxSize = (ulong)(end - position);
                }

                if (boxSize < (ulong)headerSize || boxSize > (ulong)(end - position))
                {
                    yield break;
                }

                var boxEnd = position + (long)boxSize;
                yield return new IsoBox(Encoding.ASCII.GetString(typeBytes), position + headerSize, boxEnd);

                if (boxSize == 0)
                {
                    yield break;
                }

                position = boxEnd;
            }
        }

        private static byte[] ReadBytes(Stream stream, long position, int count)
        {
            var bytes = new byte[count];
            stream.Seek(position, SeekOrigin.Begin);
            var read = 0;
            while (read < count)
            {
                var current = stream.Read(bytes, read, count - read);
                if (current == 0)
                {
                    return null;
                }

                read += current;
            }

            return bytes;
        }

        private static int ReadByte(Stream stream, long position)
        {
            stream.Seek(position, SeekOrigin.Begin);
            return stream.ReadByte();
        }

        private static uint ReadUInt16(Stream stream, long position)
        {
            var bytes = ReadBytes(stream, position, 2);
            return bytes == null ? 0 : (uint)((bytes[0] << 8) | bytes[1]);
        }

        private static uint ReadUInt32(Stream stream, long position)
        {
            var bytes = ReadBytes(stream, position, 4);
            return bytes == null ? 0 : ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        }

        private static ulong ReadUInt64(Stream stream, long position)
        {
            return ((ulong)ReadUInt32(stream, position) << 32) | ReadUInt32(stream, position + 4);
        }

        private readonly struct IsoBox
        {
            public IsoBox(string type, long payloadStart, long end)
            {
                Type = type;
                PayloadStart = payloadStart;
                End = end;
            }

            public string Type { get; }

            public long PayloadStart { get; }

            public long End { get; }
        }

        internal readonly struct AlacConfiguration
        {
            public AlacConfiguration(int bitDepth, uint bitRate, uint sampleRate)
            {
                BitDepth = bitDepth;
                BitRate = bitRate;
                SampleRate = sampleRate;
            }

            public int BitDepth { get; }

            public uint BitRate { get; }

            public uint SampleRate { get; }
        }
    }
}
