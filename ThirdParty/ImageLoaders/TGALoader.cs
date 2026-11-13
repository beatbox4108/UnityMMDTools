// This was made by aaro4130 on the Unity forums.  Thanks boss!
// It's been optimized and slimmed down for the purpose of loading Quake 3 TGA textures from memory streams.

using System.IO;
using UnityEngine;

namespace UMT.ThirdParty
{
    public static class TGALoader
    {
        public static Texture2D LoadTGA(string fileName)
        {
            using (var imageFile = File.OpenRead(fileName))
            {
                return LoadTGA(imageFile);
            }
        }

        public static Texture2D LoadTGA(Stream TGAStream)
        {
            int width, height, bitDepth;
            Color32[] pulledColors = LoadTGA(TGAStream, out width, out height, out bitDepth);

            Texture2D tex = new Texture2D(width, height);

            tex.SetPixels32(pulledColors);
            tex.Apply();
            return tex;
        }

        public static Color32[] LoadTGA(Stream TGAStream, out int width, out int height, out int bitDepth)
        {
            using (BinaryReader r = new BinaryReader(TGAStream))
            {
                // The caller may have left the stream at the end of the file.
                r.BaseStream.Seek(0, SeekOrigin.Begin);
                int idLength = r.ReadByte();
                int colorMapType = r.ReadByte();
                int imageType = r.ReadByte();

                // Skip the color map specification and the image's screen position.
                r.BaseStream.Seek(9, SeekOrigin.Current);
                width = r.ReadUInt16();
                height = r.ReadUInt16();
                bitDepth = r.ReadByte();
                int descriptor = r.ReadByte();

                bool trueColor = imageType == 2 || imageType == 10;
                bool grayscale = imageType == 3 || imageType == 11;
                if (colorMapType != 0 || (!trueColor && !grayscale))
                {
                    throw new InvalidDataException("Only true-color and grayscale TGA images without color maps are supported.");
                }
                if ((trueColor && bitDepth != 24 && bitDepth != 32) || (grayscale && bitDepth != 8))
                {
                    throw new InvalidDataException("TGA images must use 24/32-bit true color or 8-bit grayscale.");
                }
                if (width == 0 || height == 0 || (long)width * height > int.MaxValue)
                {
                    throw new InvalidDataException("TGA image dimensions are invalid or too large.");
                }
                if ((descriptor & 0xC0) != 0)
                {
                    throw new InvalidDataException("Interleaved TGA images are not supported.");
                }
                if (r.ReadBytes(idLength).Length != idLength)
                {
                    throw new EndOfStreamException("TGA image ID is truncated.");
                }

                Color32[] pulledColors = new Color32[width * height];
                bool rle = imageType == 10 || imageType == 11;
                bool rightOrigin = (descriptor & 0x10) != 0;
                bool topOrigin = (descriptor & 0x20) != 0;
                int pixelIndex = 0;
                while (pixelIndex < pulledColors.Length)
                {
                    int packetHeader = rle ? r.ReadByte() : 0;
                    int packetLength = rle ? (packetHeader & 0x7F) + 1 : pulledColors.Length;
                    bool repeated = (packetHeader & 0x80) != 0;
                    if (packetLength > pulledColors.Length - pixelIndex)
                    {
                        throw new InvalidDataException("TGA RLE packet exceeds the image dimensions.");
                    }

                    Color32 color = default;
                    for (int i = 0; i < packetLength; i++, pixelIndex++)
                    {
                        if (!repeated || i == 0)
                        {
                            color = ReadColor(r, bitDepth);
                        }

                        // Unity's pixel array starts at the bottom-left corner.
                        int x = pixelIndex % width;
                        int y = pixelIndex / width;
                        if (rightOrigin) x = width - 1 - x;
                        if (topOrigin) y = height - 1 - y;
                        pulledColors[y * width + x] = color;
                    }
                }

                return pulledColors;
            }
        }

        private static Color32 ReadColor(BinaryReader reader, int bitDepth)
        {
            byte blue = reader.ReadByte();
            if (bitDepth == 8)
            {
                return new Color32(blue, blue, blue, 255);
            }

            byte green = reader.ReadByte();
            byte red = reader.ReadByte();
            byte alpha = bitDepth == 32 ? reader.ReadByte() : (byte)255;
            return new Color32(red, green, blue, alpha);
        }
    }
}
