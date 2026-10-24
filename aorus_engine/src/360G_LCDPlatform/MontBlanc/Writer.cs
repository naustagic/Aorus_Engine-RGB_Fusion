using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Holtek;
using SevenZip;

namespace MontBlanc;

internal class Writer : ICodeProgress
{
	private MemoryStream ms;

	private List<byte[]> additionalData;

	private long totalSize = 0L;

	private IProgress progress = null;

	public Writer()
	{
		ms = new MemoryStream();
		additionalData = new List<byte[]>();
	}

	private byte[] readFile(string filePath)
	{
		FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		byte[] array = new byte[fileStream.Length];
		int num = fileStream.Read(array, 0, (int)fileStream.Length);
		if (num == fileStream.Length)
		{
			fileStream.Close();
			return array;
		}
		fileStream.Close();
		return null;
	}

	public bool addExeFile(string filePath)
	{
		byte[] array = readFile(filePath);
		byte[] array2 = new byte[4];
		array2 = BitConverter.GetBytes(array.Length);
		for (int i = 0; i < array2.Length; i++)
		{
			array[i + 28] = array2[i];
		}
		if (array != null && array.Length > 0)
		{
			ms.Write(array, 0, array.Length);
			return true;
		}
		return false;
	}

	public bool addAdditionalFile(string filePath)
	{
		if (filePath != null)
		{
			additionalData.Add(readFile(filePath));
		}
		else
		{
			byte[] item = new byte[4];
			additionalData.Add(item);
		}
		return true;
	}

	public bool addAdditionalFile(byte[] data)
	{
		if (data != null)
		{
			additionalData.Add(data);
		}
		else
		{
			byte[] item = new byte[4];
			additionalData.Add(item);
		}
		return true;
	}

	public bool output(string filePath, IProgress p)
	{
		progress = p;
		if (filePath != null && filePath.Length > 0)
		{
			Stream stream = new MemoryStream();
			FileStream fileStream = null;
			try
			{
				fileStream = new FileStream(filePath, FileMode.Create);
			}
			catch (Exception ex)
			{
				ex.ToString();
				stream = null;
				fileStream = null;
				return false;
			}
			byte[] bytes = BitConverter.GetBytes(additionalData.Count);
			stream.Write(bytes, 0, bytes.Length);
			foreach (byte[] additionalDatum in additionalData)
			{
				if (additionalDatum.Length != 4)
				{
					bytes = BitConverter.GetBytes(additionalDatum.Length);
					stream.Write(bytes, 0, bytes.Length);
					stream.Write(additionalDatum, 0, additionalDatum.Length);
				}
				else
				{
					bytes = BitConverter.GetBytes(0);
					stream.Write(bytes, 0, bytes.Length);
				}
			}
			IDataManager dataManager = new LCDPlatformDataManager();
			totalSize = stream.Length;
			stream.Seek(0L, SeekOrigin.Begin);
			stream = dataManager.compress(stream, this);
			stream = dataManager.crypto(stream, new byte[1] { 90 });
			ms.Write(((MemoryStream)stream).GetBuffer(), 0, (int)stream.Length);
			fileStream.Write(ms.GetBuffer(), 0, (int)ms.Length);
			fileStream.Flush();
			fileStream.Close();
			return true;
		}
		return false;
	}

	public bool isTinyProgramer()
	{
		string executablePath = Application.ExecutablePath;
		FileStream fileStream = new FileStream(executablePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		byte[] array = new byte[128];
		int num = fileStream.Read(array, 0, 128);
		if (num > 0)
		{
			int num2 = 0;
			if (BitConverter.ToInt32(array, 28) != 0)
			{
				fileStream.Close();
				return true;
			}
			fileStream.Close();
			return false;
		}
		fileStream.Close();
		return false;
	}

	public void SetProgress(long inSize, long outSize)
	{
		if (progress != null)
		{
			int percent = 0;
			if (inSize > 0 && totalSize > 0)
			{
				percent = (int)(inSize * 100 / totalSize);
			}
			progress.updateProgress(percent, HT32F.STAGE_OTHER);
		}
	}
}
