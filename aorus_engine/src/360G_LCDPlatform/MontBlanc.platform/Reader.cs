using System;
using System.Collections.Generic;
using System.IO;
using Holtek;

namespace MontBlanc.platform;

internal class Reader
{
	public const int INDEX_IMAGE = 0;

	public const int INDEX_AP = 1;

	public const int INDEX_VERSION = 2;

	public const int INDEX_USB_DLL = 3;

	public const int INDEX_SCSI_DLL = 4;

	public const int INDEX_CSKIN = 5;

	private string filePath;

	private Dictionary<int, byte[]> datas = null;

	public static Reader platformReader = null;

	private Reader(string filePath)
	{
		datas = new Dictionary<int, byte[]>();
		this.filePath = filePath;
		FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		byte[] array = new byte[32];
		fileStream.Read(array, 0, 32);
		uint num = BitConverter.ToUInt32(array, 28);
		if (num == 0)
		{
			return;
		}
		byte[] array2 = new byte[fileStream.Length - num];
		fileStream.Seek(num, SeekOrigin.Begin);
		fileStream.Read(array2, 0, array2.Length);
		fileStream.Close();
		fileStream = null;
		Stream stream = new MemoryStream();
		stream.Write(array2, 0, array2.Length);
		stream.Seek(0L, SeekOrigin.Begin);
		IDataManager dataManager = new LCDPlatformDataManager();
		stream = dataManager.decrypt(stream, new byte[1] { 90 });
		stream = dataManager.decompress(stream);
		byte[] array3 = new byte[4];
		stream.Read(array3, 0, array3.Length);
		uint num2 = BitConverter.ToUInt32(array3, 0);
		uint num3 = 0u;
		for (int i = 0; i < num2; i++)
		{
			stream.Read(array3, 0, array3.Length);
			num3 = BitConverter.ToUInt32(array3, 0);
			if (datas != null && num3 != 0 && num3 < 134217728)
			{
				byte[] array4 = new byte[num3];
				stream.Read(array4, 0, (int)num3);
				datas.Add(i, array4);
			}
		}
	}

	public static Reader getInstance(string filePath)
	{
		if (platformReader == null)
		{
			platformReader = new Reader(filePath);
		}
		return platformReader;
	}

	public byte[] getBinData(int index)
	{
		if (datas != null)
		{
			byte[] value = null;
			if (datas.TryGetValue(index, out value))
			{
				return value;
			}
			return null;
		}
		return null;
	}
}
