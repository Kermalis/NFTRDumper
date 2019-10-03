using Kermalis.EndianBinaryIO;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Kermalis.NFTRDumper
{
    internal sealed class Program
    {
        private sealed class Character
        {
            public byte SpaceWidth;
            public byte Width;
            public byte[][] Bitmap;

            public Character(EndianBinaryReader r, byte bpp, byte maxWidth, byte height)
            {
                SpaceWidth = r.ReadByte(); // Width of transparency after the char
                Width = r.ReadByte(); // Width of this char
                r.ReadByte(); // ?

                int curBit = 0;
                byte curByte = 0;
                byte[][] bitmap = new byte[height][];
                for (int y = 0; y < height; y++)
                {
                    byte[] arrY = new byte[Width];
                    for (int x = 0; x < maxWidth; x++)
                    {
                        if (curBit == 0)
                        {
                            curByte = r.ReadByte();
                        }
                        if (x < Width)
                        {
                            arrY[x] = (byte)((curByte >> (8 - bpp - curBit)) % (1 << bpp));
                        }
                        curBit = (curBit + bpp) % 8;
                    }
                    bitmap[y] = arrY;
                }
                Bitmap = bitmap;
            }
        }

        private static void ShowUsage()
        {
            Console.WriteLine("Invalid arguments. Proper usage:");
            Console.WriteLine("\tdotnet NFTRDumper.dll inputPath outputPath (--png color0,color1,..)");
            Console.WriteLine();
            Console.WriteLine("Example usage:");
            Console.WriteLine("\tdotnet NFTRDumper.dll \"NFTRs\\0.nftr\" \"NFTRs\\0.kermfont\"");
            Console.WriteLine("\tdotnet NFTRDumper.dll \"NFTRs\\0.nftr\" \"NFTRs\\0 PNGs\" --png 00000000,FFFFFFFF,FF000000,FF808080");
            Console.WriteLine();
            Console.WriteLine("Argument 0 is the input path.");
            Console.WriteLine("Argument 1 is the output path.");
            Console.WriteLine("(Optional) Argument 2 specifies exporting chars as PNG files. If argument 2 is omitted, then the font will export as a \"Kermalis font file\" (see https://github.com/Kermalis/NFTRDumper to learn the format.)");
            Console.WriteLine("(Optional) Argument 3 specifies the colors to use with PNG exporting. If argument 2 is supplied, then argument 3 must also be supplied.");
        }

        public static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                ShowUsage();
                return;
            }
            bool png = false;
            Color[] pngColors = null;
            if (args.Length > 2)
            {
                if (args.Length == 4 && args[2].Equals("--png", StringComparison.OrdinalIgnoreCase))
                {
                    string[] colors = args[3].Split(',');
                    pngColors = new Color[colors.Length];
                    for (int i = 0; i < colors.Length; i++)
                    {
                        pngColors[i] = Color.FromArgb(Convert.ToInt32(colors[i], 16));
                    }
                    png = true;
                }
                else
                {
                    ShowUsage();
                    return;
                }
            }
            using (var r = new EndianBinaryReader(File.OpenRead(args[0]), Endianness.BigEndian))
            {
                // RTFN (Nitro Font Resource)
                r.ReadString(4); // "RTFN"
                r.Endianness = r.ReadUInt16() == 0xFFFE ? Endianness.LittleEndian : Endianness.BigEndian;
                r.ReadUInt16(); // ?
                r.ReadUInt32(); // File size
                r.ReadUInt16(); // Block size
                r.ReadUInt16(); // NumBlocks

                // FINF (Font Info)
                r.ReadString(4); // "FNIF"
                uint FNIFSize = r.ReadUInt32();
                r.ReadByte(); // ?
                byte height = r.ReadByte(); // Height
                r.ReadUInt16(); // Null char index
                r.ReadByte(); // ?
                byte maxWidth = r.ReadByte();
                r.ReadByte(); // Width 2
                r.ReadByte(); // Encoding (00 utf8, 01 utf16, but why does it matter?)
                uint PLGCOffset = r.ReadUInt32();
                r.ReadUInt32(); // HDWC offset
                uint PAMCOffset = r.ReadUInt32();
                if (FNIFSize == 0x20)
                {
                    r.ReadByte(); // Height 2
                    r.ReadByte(); // Width 3
                    r.ReadByte(); // "Bearing Y"
                    r.ReadByte(); // "Bearing X"
                }

                // PLGC (Character Glyphs)
                r.BaseStream.Position = PLGCOffset - 0x8;
                r.ReadString(4); // "PLGC"
                uint PLGCSize = r.ReadUInt32();
                r.ReadByte(); // Width 4
                r.ReadByte(); // Height 3
                ushort lengthPerChar = r.ReadUInt16();
                r.ReadUInt16(); // ?
                byte bpp = r.ReadByte();
                r.ReadByte(); // Orientation
                var chars = new Character[(PLGCSize - 0x10) / lengthPerChar];
                for (int i = 0; i < chars.Length; i++)
                {
                    chars[i] = new Character(r, bpp, maxWidth, height);
                }

                // HDWC (Literally no idea so let's skip it since it has nothing useful)

                // PAMC (Character Map)
                var dict = new Dictionary<ushort, ushort>(); // Key is the char code, Value is the char index                                                             
                long nextPAMCOffset = PAMCOffset - 0x8; // Each PAMC points to the next until there is no valid offset left
                while (nextPAMCOffset < r.BaseStream.Length)
                {
                    r.BaseStream.Position = nextPAMCOffset;
                    r.ReadBytes(4); // "PAMC"
                    r.ReadUInt32(); // PAMCSize
                    ushort firstCharCode = r.ReadUInt16();
                    ushort lastCharCode = r.ReadUInt16();
                    uint type = r.ReadUInt32();
                    nextPAMCOffset = r.ReadUInt32() - 0x8;
                    switch (type)
                    {
                        case 0:
                        {
                            ushort charIndex = r.ReadUInt16();
                            for (ushort i = firstCharCode; i <= lastCharCode; i++)
                            {
                                ushort value = charIndex++;
                                if (value != ushort.MaxValue)
                                {
                                    dict.Add(i, value);
                                }
                            }
                            break;
                        }
                        case 1:
                        {
                            for (ushort i = firstCharCode; i <= lastCharCode; i++)
                            {
                                ushort value = r.ReadUInt16();
                                if (value != ushort.MaxValue)
                                {
                                    dict.Add(i, value);
                                }
                            }
                            break;
                        }
                        case 2:
                        {
                            ushort numDefinitions = r.ReadUInt16();
                            for (ushort i = 0; i < numDefinitions; ++i)
                            {
                                ushort key = r.ReadUInt16();
                                ushort value = r.ReadUInt16();
                                if (value != ushort.MaxValue)
                                {
                                    dict.Add(key, value);
                                }
                            }
                            break;
                        }
                        default: throw new InvalidDataException();
                    }
                }

                // Now we can save
                if (png)
                {
                    Directory.CreateDirectory(args[1]);
                    foreach (KeyValuePair<ushort, ushort> pair in dict)
                    {
                        Character car = chars[pair.Value];
                        using (var bmp = new Bitmap(car.Width + car.SpaceWidth, height))
                        {
                            for (int y = 0; y < height; y++)
                            {
                                byte[] arrY = car.Bitmap[y];
                                for (int x = 0; x < car.Width; x++)
                                {
                                    int colorIndex = arrY[x];
                                    if (colorIndex >= pngColors.Length)
                                    {
                                        Console.WriteLine("Not enough colors supplied! Missing color index: " + colorIndex);
                                        return;
                                    }
                                    bmp.SetPixel(x, y, pngColors[colorIndex]);
                                }
                            }
                            bmp.Save(Path.Combine(args[1], pair.Key.ToString("X4") + ".png"));
                        }
                    }
                }
                else
                {
                    using (var w = new EndianBinaryWriter(File.Create(args[1]), Endianness.LittleEndian))
                    {
                        w.Write(height);
                        w.Write(bpp);
                        w.Write(dict.Count);
                        foreach (KeyValuePair<ushort, ushort> pair in dict)
                        {
                            w.Write(pair.Key);
                            Character car = chars[pair.Value];
                            w.Write(car.Width);
                            w.Write(car.SpaceWidth);

                            int curBit = 0;
                            byte curByte = 0;
                            for (int y = 0; y < height; y++)
                            {
                                byte[] arrY = car.Bitmap[y];
                                for (int x = 0; x < car.Width; x++)
                                {
                                    curByte |= (byte)(arrY[x] << (8 - bpp - curBit));
                                    curBit = (curBit + bpp) % 8;
                                    if (curBit == 0)
                                    {
                                        w.Write(curByte);
                                        curByte = 0;
                                    }
                                }
                            }
                            if (curBit != 0)
                            {
                                w.Write(curByte);
                            }
                        }
                    }
                }
            }
        }
    }
}
