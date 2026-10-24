using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AorusCoolerConverter.Core;

public class AorusBinOptions
{
    public int TargetWidth { get; set; } = 320;
    public int TargetHeight { get; set; } = 320;
    public int RotationAngle { get; set; } = 0; // 0, 90, 180, 270
    public bool ByteSwapEndianness { get; set; } = false; // true if MSB first
    public Color BackgroundColor { get; set; } = Color.Black;
    public int MaxGifFrames { get; set; } = 150;
}

public class AorusFrameInfo
{
    public int Width { get; set; } = 320;
    public int Height { get; set; } = 320;
    public ushort Format { get; set; } = 1; // 1 = RAW RGB565
    public byte[] PixelData { get; set; } = Array.Empty<byte>();
}

public class GitHubReleaseInfo
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = "";

    public bool HasUpdate { get; set; }
}

public static class AorusImageProcessor
{
    public const int DefaultDisplayWidth = 320;
    public const int DefaultDisplayHeight = 320;
    public const int FrameSizeBytes = DefaultDisplayWidth * DefaultDisplayHeight * 2; // 204,800 bytes

    /// <summary>
    /// Converts any image file (PNG, JPG, BMP, GIF animado) to an AORUS cooler .bin payload.
    /// </summary>
    public static byte[] ProcessImageToBin(string imagePath, AorusBinOptions? options = null)
    {
        options ??= new AorusBinOptions();

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException($"Imagem não encontrada: {imagePath}");
        }

        using var srcImage = Image.FromFile(imagePath);
        var frames = ExtractAndConvertFrames(srcImage, options);
        return BuildAorusBin(frames);
    }

    /// <summary>
    /// Converts a bitmap into an array of frames, handling static images and animated GIFs.
    /// </summary>
    public static List<AorusFrameInfo> ExtractAndConvertFrames(Image image, AorusBinOptions options)
    {
        var frames = new List<AorusFrameInfo>();
        var frameDim = new FrameDimension(image.FrameDimensionsList[0]);
        int frameCount = image.GetFrameCount(frameDim);

        int totalToExtract = Math.Min(frameCount, options.MaxGifFrames);
        for (int i = 0; i < totalToExtract; i++)
        {
            image.SelectActiveFrame(frameDim, i);
            using var frameBmp = new Bitmap(image);
            using var resizedBmp = PrepareFrame(frameBmp, options);
            var rgb565 = ConvertToRgb565Bytes(resizedBmp, options.ByteSwapEndianness);

            frames.Add(new AorusFrameInfo
            {
                Width = options.TargetWidth,
                Height = options.TargetHeight,
                Format = 1,
                PixelData = rgb565
            });
        }

        return frames;
    }

    public static Bitmap PrepareFrame(Bitmap src, int width, int height)
    {
        return PrepareFrame(src, new AorusBinOptions { TargetWidth = width, TargetHeight = height });
    }

    /// <summary>
    /// Resizes, centers and pads image onto a solid background (for circular LCDs), applying rotation.
    /// </summary>
    public static Bitmap PrepareFrame(Bitmap src, AorusBinOptions options)
    {
        var dest = new Bitmap(options.TargetWidth, options.TargetHeight, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(dest))
        {
            g.Clear(options.BackgroundColor);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Apply rotation around center
            if (options.RotationAngle != 0)
            {
                g.TranslateTransform(options.TargetWidth / 2f, options.TargetHeight / 2f);
                g.RotateTransform(options.RotationAngle);
                g.TranslateTransform(-options.TargetWidth / 2f, -options.TargetHeight / 2f);
            }

            // Fit aspect ratio cleanly
            float scale = Math.Min((float)options.TargetWidth / src.Width, (float)options.TargetHeight / src.Height);
            int newWidth = (int)(src.Width * scale);
            int newHeight = (int)(src.Height * scale);
            int posX = (options.TargetWidth - newWidth) / 2;
            int posY = (options.TargetHeight - newHeight) / 2;

            g.DrawImage(src, new Rectangle(posX, posY, newWidth, newHeight));
        }
        return dest;
    }

    /// <summary>
    /// Converts a 32bppArgb Bitmap to exact RGB565 byte buffer (2 bytes per pixel).
    /// </summary>
    public static byte[] ConvertToRgb565Bytes(Bitmap bmp, bool byteSwap = false)
    {
        int width = bmp.Width;
        int height = bmp.Height;
        byte[] result = new byte[width * height * 2];

        var rect = new Rectangle(0, 0, width, height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        unsafe
        {
            byte* scan0 = (byte*)data.Scan0;
            int stride = data.Stride;

            int dstIdx = 0;
            for (int y = 0; y < height; y++)
            {
                byte* row = scan0 + (y * stride);
                for (int x = 0; x < width; x++)
                {
                    byte b = row[x * 4 + 0];
                    byte g = row[x * 4 + 1];
                    byte r = row[x * 4 + 2];

                    // Convert to 5-6-5
                    ushort r5 = (ushort)(r >> 3);
                    ushort g6 = (ushort)(g >> 2);
                    ushort b5 = (ushort)(b >> 3);
                    ushort rgb565 = (ushort)((r5 << 11) | (g6 << 5) | b5);

                    byte bLow = (byte)(rgb565 & 0xFF);
                    byte bHigh = (byte)((rgb565 >> 8) & 0xFF);

                    if (byteSwap)
                    {
                        result[dstIdx++] = bHigh;
                        result[dstIdx++] = bLow;
                    }
                    else
                    {
                        result[dstIdx++] = bLow;
                        result[dstIdx++] = bHigh;
                    }
                }
            }
        }

        bmp.UnlockBits(data);
        return result;
    }

    /// <summary>
    /// Packages frames into the exact AORUS AIO binary format:
    /// [ushort frameCount] + [10 bytes per frame table: uint endOffset, ushort w, ushort h, ushort fmt] + [payloads]
    /// </summary>
    public static byte[] BuildAorusBin(List<AorusFrameInfo> frames)
    {
        if (frames.Count == 0)
        {
            throw new ArgumentException("Pelo menos um quadro deve ser fornecido.");
        }

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // 1. Frame count (2 bytes)
        ushort count = (ushort)frames.Count;
        writer.Write(count);

        // 2. Table of entries (10 bytes each)
        // Offset starts after header: 2 + (count * 10)
        uint runningOffset = (uint)(2 + (count * 10));

        foreach (var frame in frames)
        {
            runningOffset += (uint)frame.PixelData.Length;
            uint endOffset = runningOffset - 1;

            writer.Write(endOffset);           // 4 bytes: end offset
            writer.Write((ushort)frame.Width);  // 2 bytes: width
            writer.Write((ushort)frame.Height); // 2 bytes: height
            writer.Write(frame.Format);         // 2 bytes: format (1)
        }

        // 3. Pixel payload data
        foreach (var frame in frames)
        {
            writer.Write(frame.PixelData);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Extracts frames from an existing AORUS .bin file (e.g. 0.bin) back into Bitmaps.
    /// </summary>
    public static List<Bitmap> ExtractFramesFromBin(byte[] binData, bool byteSwap = false)
    {
        using var ms = new MemoryStream(binData);
        using var reader = new BinaryReader(ms);

        ushort count = reader.ReadUInt16();
        var table = new List<(uint endOffset, ushort w, ushort h, ushort fmt)>();

        for (int i = 0; i < count; i++)
        {
            uint end = reader.ReadUInt32();
            ushort w = reader.ReadUInt16();
            ushort h = reader.ReadUInt16();
            ushort fmt = reader.ReadUInt16();
            table.Add((end, w, h, fmt));
        }

        var bitmaps = new List<Bitmap>();
        uint prevOffset = (uint)(2 + count * 10);

        foreach (var entry in table)
        {
            uint len = (entry.endOffset + 1) - prevOffset;
            byte[] pixelBytes = reader.ReadBytes((int)len);
            prevOffset = entry.endOffset + 1;

            var bmp = new Bitmap(entry.w, entry.h, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, entry.w, entry.h);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* scan0 = (byte*)data.Scan0;
                int stride = data.Stride;
                int srcIdx = 0;

                for (int y = 0; y < entry.h; y++)
                {
                    byte* row = scan0 + (y * stride);
                    for (int x = 0; x < entry.w; x++)
                    {
                        if (srcIdx + 1 < pixelBytes.Length)
                        {
                            byte b1 = pixelBytes[srcIdx++];
                            byte b2 = pixelBytes[srcIdx++];
                            ushort raw = byteSwap ? (ushort)((b1 << 8) | b2) : (ushort)((b2 << 8) | b1);

                            // Expand RGB565 to 888
                            byte r = (byte)(((raw >> 11) & 0x1F) * 255 / 31);
                            byte g = (byte)(((raw >> 5) & 0x3F) * 255 / 63);
                            byte b = (byte)((raw & 0x1F) * 255 / 31);

                            row[x * 4 + 0] = b;
                            row[x * 4 + 1] = g;
                            row[x * 4 + 2] = r;
                            row[x * 4 + 3] = 255;
                        }
                    }
                }
            }

            bmp.UnlockBits(data);
            bitmaps.Add(bmp);
        }

        return bitmaps;
    }

    public const string DefaultGitHubRepo = "naustagic/Aorus_Engine-RGB_Fusion";

    /// <summary>
    /// Checks GitHub Releases API for new updates.
    /// </summary>
    public static async Task<GitHubReleaseInfo> CheckForGitHubUpdatesAsync(string? ownerRepo, string currentVersion)
    {
        var result = new GitHubReleaseInfo();
        if (string.IsNullOrWhiteSpace(ownerRepo))
        {
            ownerRepo = DefaultGitHubRepo;
        }

        if (!ownerRepo.Contains('/'))
        {
            return result;
        }

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "AorusCoolerConverter-Updater");
            string url = $"https://api.github.com/repos/{ownerRepo.Trim()}/releases/latest";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return result;
            }

            var release = await response.Content.ReadFromJsonAsync<GitHubReleaseInfo>();
            if (release != null && !string.IsNullOrWhiteSpace(release.TagName))
            {
                result = release;
                string latest = release.TagName.TrimStart('v', 'V');
                string current = currentVersion.TrimStart('v', 'V');
                result.HasUpdate = !string.Equals(latest, current, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // Network errors or invalid repo strings fail gracefully without crashing
        }

        return result;
    }
}
