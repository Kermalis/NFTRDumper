This is a program that can dump a NFTR font's glyphs.

Example usage:
* `dotnet NFTRDumper.dll "NFTRs\0.nftr" "NFTRs\0.kermfont"`
* `dotnet NFTRDumper.dll "NFTRs\0.nftr" "NFTRs\0 PNGs" --png 00000000,FFFFFFFF,FF000000,FF808080`

Exporting the glyphs to PNG format requires that you supply hex codes for the colors.
If the `--png` argument is omitted, then the output file will be of the following format (little-endian, no padding/alignment):

```
(u8) FontHeight // The amount of pixels tall each glyph is
(u8) BitsPerPixel // The amount of bits that define the color of each pixel
(s32) NumGlyphs // The amount of glyphs to read next
{ // Glyphs
  (u16) CharCode // The UTF-16 character code of this glyph
  (u8) CharWidth // The amount of pixels wide this glyph is
  (u8) CharSpace // The amount of transparent horizontal pixels at the end of this glyph
  (u8[]) Bitmap // The variable-length array that contains the x,y-ordered bits for each pixel
                // The amount of bits to read is (FontHeight * CharWidth * BitsPerPixel)
                // Therefore, the array length is ((NumBitsToRead / 8) + ((NumBitsToRead % 8) != 0 ? 1 : 0))
}
```

----
# NFTRDumper uses:
* [My EndianBinaryIO library](https://github.com/Kermalis/EndianBinaryIO)