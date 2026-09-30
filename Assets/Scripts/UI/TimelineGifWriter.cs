using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Horizon.UI
{
    // GIF89a with a small global palette and LZW compression. Frames come from
    // the actual Unity canvas; no server, video encoder or asset download is used.
    public sealed class TimelineGifWriter : IDisposable
    {
        private readonly BinaryWriter writer;
        private readonly int width, height;
        private readonly byte[] red = new byte[256], green = new byte[256], blue = new byte[256];
        private bool disposed;

        public TimelineGifWriter(Stream stream, int width, int height)
        {
            if (width < 1 || width > 4096 || height < 1 || height > 4096) throw new ArgumentOutOfRangeException();
            this.width = width; this.height = height;
            writer = new BinaryWriter(stream, Encoding.ASCII, true);
            writer.Write(Encoding.ASCII.GetBytes("GIF89a"));
            writer.Write((ushort)width); writer.Write((ushort)height);
            writer.Write((byte)0xf7); writer.Write((byte)0); writer.Write((byte)0);
            int[] shades = { 0, 8, 20, 38, 68, 110, 175, 255 }, blues = { 0, 20, 80, 200 };
            for (int i = 0; i < 256; i++)
            { writer.Write((byte)shades[i >> 5]); writer.Write((byte)shades[(i >> 2) & 7]); writer.Write((byte)blues[i & 3]); }
            for (int i = 0; i < 256; i++)
            { red[i] = (byte)(Nearest(i, shades) << 5); green[i] = (byte)(Nearest(i, shades) << 2); blue[i] = (byte)Nearest(i, blues); }
            writer.Write(new byte[] { 0x21, 0xff, 11 }); writer.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
            writer.Write(new byte[] { 3, 1, 0, 0, 0 });
        }

        private static int Nearest(int value, int[] levels)
        {
            int best = 0;
            for (int i = 1; i < levels.Length; i++) if (Math.Abs(value - levels[i]) < Math.Abs(value - levels[best])) best = i;
            return best;
        }

        public void Frame(Color32[] bottomUpPixels, int centiseconds)
        {
            if (disposed || bottomUpPixels == null || bottomUpPixels.Length != width * height)
                throw new ArgumentException("Frame dimensions do not match the recording.");
            writer.Write(new byte[] { 0x21, 0xf9, 4, 4 }); writer.Write((ushort)Math.Max(1, centiseconds));
            writer.Write((byte)0); writer.Write((byte)0);
            writer.Write((byte)0x2c); writer.Write((ushort)0); writer.Write((ushort)0);
            writer.Write((ushort)width); writer.Write((ushort)height); writer.Write((byte)0);
            var indices = new byte[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Color32 pixel = bottomUpPixels[(height - y - 1) * width + x];
                    indices[y * width + x] = (byte)(red[pixel.r] | green[pixel.g] | blue[pixel.b]);
                }
            byte[] compressed = Compress(indices);
            writer.Write((byte)8);
            for (int offset = 0; offset < compressed.Length; offset += 255)
            { int count = Math.Min(255, compressed.Length - offset); writer.Write((byte)count); writer.Write(compressed, offset, count); }
            writer.Write((byte)0);
        }

        private static byte[] Compress(byte[] pixels)
        {
            var table = new Dictionary<int, int>(4096);
            var output = new MemoryStream();
            int bits = 9, nextCode = 258, bitCount = 0;
            uint buffer = 0;
            Action<int, int> emit = (code, size) =>
            {
                buffer |= (uint)code << bitCount; bitCount += size;
                while (bitCount >= 8) { output.WriteByte((byte)buffer); buffer >>= 8; bitCount -= 8; }
            };
            emit(256, bits);
            int prefix = pixels[0];
            for (int i = 1; i < pixels.Length; i++)
            {
                int current = pixels[i], key = (prefix << 8) | current;
                if (table.TryGetValue(key, out int existing)) { prefix = existing; continue; }
                emit(prefix, bits);
                table[key] = nextCode++;
                if (nextCode > (1 << bits) && bits < 12) bits++;
                if (nextCode == 4096) { emit(256, bits); table.Clear(); bits = 9; nextCode = 258; }
                prefix = current;
            }
            emit(prefix, bits);
            if (nextCode == (1 << bits) && bits < 12) bits++;
            emit(257, bits);
            if (bitCount > 0) output.WriteByte((byte)buffer);
            return output.ToArray();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; writer.Write((byte)0x3b); writer.Flush(); writer.Dispose();
        }
    }
}
