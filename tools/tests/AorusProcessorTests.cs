using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using AorusCoolerConverter.Core;
using Xunit;

namespace tests;

public class AorusProcessorTests
{
    [Fact]
    public void Test_ConvertStaticImageToBin_HeaderAndSizeValid()
    {
        // Create an in-memory 500x300 image with alpha
        using var bmp = new Bitmap(500, 300, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(128, 255, 0, 0)); // Semi-transparent red
        }

        string tempPath = Path.Combine(Path.GetTempPath(), "test_static_image.png");
        try
        {
            bmp.Save(tempPath, ImageFormat.Png);

            var opts = new AorusBinOptions { TargetWidth = 320, TargetHeight = 320 };
            byte[] binData = AorusImageProcessor.ProcessImageToBin(tempPath, opts);

            // Assertions on the Aorus BIN format
            Assert.NotNull(binData);
            Assert.True(binData.Length > 2 + 10);

            // Check frame count
            ushort frameCount = BitConverter.ToUInt16(binData, 0);
            Assert.Equal(1, frameCount);

            // Check 10-byte table entry
            uint endOffset = BitConverter.ToUInt32(binData, 2);
            ushort width = BitConverter.ToUInt16(binData, 6);
            ushort height = BitConverter.ToUInt16(binData, 8);
            ushort format = BitConverter.ToUInt16(binData, 10);

            Assert.Equal(320, width);
            Assert.Equal(320, height);
            Assert.Equal(1, format);

            // Expected size: 2 bytes count + (1 * 10) bytes header + (320 * 320 * 2) bytes payload = 204,812 bytes
            int expectedTotal = 2 + 10 + (320 * 320 * 2);
            Assert.Equal(expectedTotal, binData.Length);
            Assert.Equal((uint)(expectedTotal - 1), endOffset);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Test_ExtractFromOriginal0Bin_ValidatesCountAndDimensions()
    {
        string binPath = @"d:\eng-rev-watercooler\aorus_engine\extracted\app\AIO\images\0.bin";
        if (!File.Exists(binPath))
        {
            return; // Skip if file not found in test runner
        }

        byte[] binData = File.ReadAllBytes(binPath);
        ushort count = BitConverter.ToUInt16(binData, 0);
        Assert.Equal(129, count); // Exactly 129 frames in official Gigabyte 0.bin

        // Validate table entries
        for (int i = 0; i < count; i++)
        {
            int off = 2 + i * 10;
            ushort w = BitConverter.ToUInt16(binData, off + 4);
            ushort h = BitConverter.ToUInt16(binData, off + 6);
            ushort fmt = BitConverter.ToUInt16(binData, off + 8);

            // Entry 0 is full screen 320x320
            if (i == 0)
            {
                Assert.Equal(320, w);
                Assert.Equal(320, h);
            }
            Assert.True(w > 0 && w <= 500);
            Assert.True(h > 0 && h <= 500);
            Assert.Equal(1, fmt);
        }
    }

    [Fact]
    public void Test_Rgb565Conversion_MatchesExpectedColors()
    {
        using var bmp = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        bmp.SetPixel(0, 0, Color.FromArgb(255, 0, 0)); // Pure Red (RGB565: 0xF800 -> Little Endian: 0x00, 0xF8)

        byte[] raw = AorusImageProcessor.ConvertToRgb565Bytes(bmp, byteSwap: false);
        Assert.Equal(2, raw.Length);
        Assert.Equal(0x00, raw[0]);
        Assert.Equal(0xF8, raw[1]);
    }
}
