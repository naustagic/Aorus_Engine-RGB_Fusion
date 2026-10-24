using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using AorusCoolerConverter.Core;
using RLE_Compress;

namespace MontBlanc.Pic.Convert;

internal class BmpConvert : IPicConvert
{
	private IPicConfig config = null;

	private int imageConvertedSize_;

	private int imageWidth_;

	private int imageHeight_;

	private ushort imageFormat_;

	public int imageConvertedSize
	{
		get => imageConvertedSize_;
		set => imageConvertedSize_ = value;
	}

	public int imageWidth
	{
		get => imageWidth_;
		set => imageWidth_ = value;
	}

	public int imageHeight
	{
		get => imageHeight_;
		set => imageHeight_ = value;
	}

	public ushort imageFormat
	{
		get => imageFormat_;
		set => imageFormat_ = value;
	}

	public bool setPicConfig(IPicConfig config)
	{
		this.config = config;
		return true;
	}

	public Stream getConvertedData(ICompress icp)
	{
		imageFormat = icp.imageFormat;
		if (config == null || string.IsNullOrEmpty(config.filename) || !File.Exists(config.filename))
		{
			return null;
		}

		Bitmap preparedBitmap = null;
		try
		{
			using (var fs = new FileStream(config.filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			using (var rawImage = Image.FromStream(fs))
			using (var rawBitmap = new Bitmap(rawImage))
			{
				int targetW = (config.isResizeAll && config.resizeWidth > 0) ? config.resizeWidth : rawImage.Width;
				int targetH = (config.isResizeAll && config.resizeHeight > 0) ? config.resizeHeight : rawImage.Height;
				preparedBitmap = AorusImageProcessor.PrepareFrame(rawBitmap, targetW, targetH);
			}
		}
		catch
		{
			preparedBitmap = new Bitmap(config.filename);
		}

		imageWidth = preparedBitmap.Width;
		imageHeight = preparedBitmap.Height;

		byte[] array;
		if (config.outputType == "888")
		{
			Rectangle rect = new Rectangle(0, 0, preparedBitmap.Width, preparedBitmap.Height);
			BitmapData bitmapData = preparedBitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
			try
			{
				array = new byte[bitmapData.Stride * preparedBitmap.Height];
				Marshal.Copy(bitmapData.Scan0, array, 0, array.Length);
				if (preparedBitmap.Width * 3 != bitmapData.Stride)
				{
					byte[] compacted = new byte[preparedBitmap.Width * 3 * preparedBitmap.Height];
					for (int i = 0; i < preparedBitmap.Height; i++)
					{
						for (int j = 0; j < preparedBitmap.Width * 3; j++)
						{
							compacted[i * preparedBitmap.Width * 3 + j] = array[i * bitmapData.Stride + j];
						}
					}
					array = compacted;
				}
			}
			finally
			{
				preparedBitmap.UnlockBits(bitmapData);
			}
		}
		else
		{
			// Output RGB565 using our high-fidelity universal converter (handles PNG transparency over black background)
			array = AorusImageProcessor.ConvertToRgb565Bytes(preparedBitmap);
		}

		preparedBitmap.Dispose();
		preparedBitmap = null;

		Stream stream = null;
		if (config.isMSBFirst)
		{
			stream = icp.compress(array);
		}
		else
		{
			for (int i = 0; i < array.Length / 2; i++)
			{
				byte b = array[i * 2];
				array[i * 2] = array[i * 2 + 1];
				array[i * 2 + 1] = b;
			}
			stream = icp.compress(array);
		}

		imageConvertedSize = (int)stream.Length;
		return stream;
	}
}
