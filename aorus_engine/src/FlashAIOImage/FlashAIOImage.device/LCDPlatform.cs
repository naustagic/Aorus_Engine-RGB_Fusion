using System;

namespace FlashAIOImage.device;

internal class LCDPlatform : HT32F
{
	private uint versionSize = 1024u;

	public static uint blockSize = 65536u;

	public byte[] APBinData { get; set; }

	public byte[] versionBinData { get; set; }

	public byte[] imageBinData { get; set; }

	public int start_count { get; set; }

	public int end_count { get; set; }

	public int ap_start_count { get; set; }

	public int ap_end_count { get; set; }

	public int image_start_count { get; set; }

	public int image_end_count { get; set; }

	public double totalTime { get; set; }

	public double apSpeed { get; set; }

	public double imageSpeed { get; set; }

	public bool program(IProgress p)
	{
		start_count = Environment.TickCount;
		if (APBinData != null && APBinData.Length > 0 && versionBinData != null && versionBinData.Length > 0)
		{
			p.updateProgress(0, 1);
			bool flag = false;
			if (!eraseFlash(versionAddress, versionAddress + versionSize, p))
			{
				return false;
			}
			p.updateProgress(100, 1);
		}
		if (APBinData != null && APBinData.Length > 0 && versionBinData != null && versionBinData.Length > 0)
		{
			p.updateProgress(0, 4);
			bool flag2 = false;
			ap_start_count = Environment.TickCount;
			flag2 = programFlash(versionAddress + versionSize, versionAddress + versionSize + (uint)APBinData.Length, APBinData, p);
			ap_end_count = Environment.TickCount;
			if (!flag2)
			{
				return false;
			}
			p.updateProgress(100, 5);
			p.updateProgress(0, 6);
			if (!verify(isExtData: false, versionAddress + versionSize, (uint)APBinData.Length, APBinData, p))
			{
				p.updateProgress(100, 6);
				return false;
			}
			p.updateProgress(100, 6);
		}
		if (imageBinData != null && imageBinData.Length > 0)
		{
			image_start_count = Environment.TickCount;
			p.updateProgress(0, 7);
			eraseExtFlash(0u, (uint)imageBinData.Length, blockSize, p);
			p.updateProgress(100, 7);
			p.updateProgress(0, 8);
			bool flag3 = false;
			flag3 = programExtFlash(0u, (uint)imageBinData.Length, blockSize, imageBinData, p);
			image_end_count = Environment.TickCount;
			if (!flag3)
			{
				p.updateProgress(100, 8);
				return false;
			}
			p.updateProgress(100, 8);
			p.updateProgress(0, 9);
			if (!verify(isExtData: true, 0u, (uint)imageBinData.Length, imageBinData, p))
			{
				p.updateProgress(100, 9);
				return false;
			}
			p.updateProgress(100, 9);
		}
		if (APBinData != null && APBinData.Length > 0 && versionBinData != null && versionBinData.Length > 0)
		{
			bool flag4 = false;
			p.updateProgress(0, 2);
			if (!programFlash(versionAddress, versionAddress + versionSize, versionBinData, p))
			{
				p.updateProgress(100, 2);
				return false;
			}
			p.updateProgress(100, 2);
		}
		end_count = Environment.TickCount;
		totalTime = (double)(end_count - start_count) / 1000.0;
		if (APBinData != null && APBinData.Length > 0)
		{
			apSpeed = (double)APBinData.Length / 1024.0 / ((double)(ap_end_count - ap_start_count) / 1000.0);
		}
		if (imageBinData != null && imageBinData.Length > 0)
		{
			imageSpeed = (double)imageBinData.Length / 1024.0 / ((double)(image_end_count - image_start_count) / 1000.0);
		}
		return true;
	}

	public string getVersion()
	{
		byte[] array = new byte[12];
		char[] array2 = new char[5];
		bool flag = false;
		if (versionAddress != 0 && readFlash(versionAddress, versionAddress + (uint)array.Length, array))
		{
			for (int i = 0; i < array2.Length; i++)
			{
				array2[i] = (char)array[i + 5];
			}
			return new string(array2);
		}
		return null;
	}
}
