using System;
using System.IO;

namespace RLE_Compress;

public interface ICompress
{
    ushort imageFormat { get; set; }
    Stream compress(byte[] data);
}

public class Compress_NONE : ICompress
{
    public ushort imageFormat { get; set; } = 1;

    public Stream compress(byte[] data)
    {
        return new MemoryStream(data);
    }
}

public class Compress_RLE : ICompress
{
    public ushort imageFormat { get; set; } = 2;

    public Stream compress(byte[] data)
    {
        var ms = new MemoryStream();
        if (data == null || data.Length == 0) return ms;

        int i = 0;
        while (i < data.Length)
        {
            byte val = data[i];
            byte count = 1;
            while (i + 1 < data.Length && data[i + 1] == val && count < 255)
            {
                count++;
                i++;
            }
            ms.WriteByte(count);
            ms.WriteByte(val);
            i++;
        }

        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }
}

public class Compress_RLE2 : ICompress
{
    public ushort imageFormat { get; set; } = 3;

    public Stream compress(byte[] data)
    {
        // 16-bit word RLE
        var ms = new MemoryStream();
        if (data == null || data.Length == 0) return ms;

        int i = 0;
        while (i + 1 < data.Length)
        {
            byte b0 = data[i];
            byte b1 = data[i + 1];
            byte count = 1;

            while (i + 3 < data.Length && data[i + 2] == b0 && data[i + 3] == b1 && count < 255)
            {
                count++;
                i += 2;
            }

            ms.WriteByte(count);
            ms.WriteByte(b0);
            ms.WriteByte(b1);
            i += 2;
        }

        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }
}
