using System;
using System.IO;
using System.Management;
using FlashAIOImage.device;

namespace FlashAIOImage;

internal class HT32F : SCSI, IMcu
{
	private const byte TYPE_DIR_IN = 128;

	private const byte TYPE_DIR_OUT = 0;

	private const byte TYPE_DATA = 64;

	private const byte TYPE_NDATA = 0;

	private const byte TYPE_ROM = 1;

	private const byte TYPE_EXT_FLASH = 0;

	private const byte CMD_TEST = 241;

	private const byte CMD_GET_ID = 242;

	private const byte CMD_WRITE = 243;

	private const byte CMD_READ = 244;

	private const byte CMD_ERASE = 245;

	private const byte CMD_ALTERNATE_MODE = 246;

	public const byte MODE_AP = 0;

	public const byte MODE_IAP = 1;

	public const int STAGE_VER_ERASE = 1;

	public const int STAGE_VER_PROGRAM = 2;

	public const int STAGE_VER_VERIFY = 3;

	public const int STAGE_BIN_ERASE = 4;

	public const int STAGE_BIN_PROGRAM = 5;

	public const int STAGE_BIN_VERIFY = 6;

	public const int STAGE_IMAGE_ERASE = 7;

	public const int STAGE_IMAGE_PROGRAM = 8;

	public const int STAGE_IMAGE_VERIFY = 9;

	public static readonly int STAGE_OTHER;

	public string deviceName { get; set; }

	public uint versionAddress { get; set; }

	public HT32F()
	{
		versionAddress = 0u;
		findDevice();
	}

	private bool isDeviceIdAvaliable()
	{
		byte[] array = new byte[32];
		if (!sendCmd(242, 193, 0u, (uint)array.Length, array))
		{
			return false;
		}
		uint num = BitConverter.ToUInt32(array, 0);
		versionAddress = BitConverter.ToUInt32(array, 4);
		if ((num & 0xFFFF0000u) != 58064896 || versionAddress == 0 || versionAddress > 1048576)
		{
			versionAddress = 0u;
			closeDevice();
			return false;
		}
		return true;
	}

	private bool isDeviceExist(string dName)
	{
		bool flag = false;
		if (!isAvailable())
		{
			flag = openDevice(dName);
		}
		if (flag && isDeviceIdAvaliable())
		{
			return true;
		}
		return false;
	}

	public bool findDevice()
	{
		bool flag = isAvailable();
		if (flag)
		{
			if (isDeviceIdAvaliable())
			{
				return true;
			}
			return false;
		}
		ManagementClass managementClass = new ManagementClass("Win32_DiskDrive");
		ManagementObjectCollection managementObjectCollection = null;
		try
		{
			managementObjectCollection = managementClass.GetInstances();
			foreach (ManagementObject item in managementObjectCollection)
			{
				if (item.Properties["Caption"] == null || item.Properties["Caption"].Value == null)
				{
					continue;
				}
				string text = item.Properties["Caption"].Value.ToString();
				if (text != null && text.Contains("LCD_PLATFORM"))
				{
					string text2 = "";
					if (item.Properties["Name"].Value == null)
					{
						break;
					}
					text2 = item.Properties["Name"].Value.ToString();
					flag = isDeviceExist(text2);
					if (flag)
					{
						deviceName = text2;
						break;
					}
				}
				else
				{
					if (item.Properties["Name"].Value == null)
					{
						break;
					}
					item.Properties["Name"].Value.ToString();
				}
			}
		}
		catch (Exception ex)
		{
			ex.ToString();
			flag = false;
		}
		finally
		{
			managementObjectCollection.Dispose();
			managementClass.Dispose();
			managementClass = null;
			managementObjectCollection = null;
		}
		return flag;
	}

	public bool eraseFlash(uint start, uint end)
	{
		bool flag = sendCmd(245, 1, start, end - start, null);
		if (flag)
		{
			testDevice();
		}
		return flag;
	}

	public bool eraseFlash(uint start, uint end, IProgress c)
	{
		bool flag = false;
		c?.updateProgress(0, STAGE_OTHER);
		flag = sendCmd(245, 1, start, end - start, null);
		if (flag)
		{
			testDevice();
		}
		c?.updateProgress(100, STAGE_OTHER);
		return flag;
	}

	public bool eraseExtFlash(uint start, uint end, uint blockSize, IProgress p)
	{
		bool result = false;
		p?.updateProgress(0, STAGE_OTHER);
		if ((end - start) % blockSize != 0)
		{
			end = (end / blockSize + 1) * blockSize + start;
		}
		for (uint num = start; num < end; num += blockSize)
		{
			long num2 = num;
			int percent = (int)(num2 * 100 / (end + 1));
			p?.updateProgress(percent, STAGE_OTHER);
			if (!sendCmd(245, 0, num, blockSize, null))
			{
				return false;
			}
		}
		testDevice();
		p?.updateProgress(100, STAGE_OTHER);
		return result;
	}

	public bool programFlash(uint start, uint end, byte[] flashData, IProgress p)
	{
		bool flag = false;
		if (eraseFlash(start, end, p))
		{
			byte[] array = new byte[64];
			flag = false;
			for (int i = 0; i < flashData.Length; i += array.Length)
			{
				uint num = start + (uint)i;
				uint num2;
				if (flashData.Length - i < array.Length)
				{
					num2 = (uint)((int)num + flashData.Length - i);
					for (int j = 0; j < flashData.Length - i; j++)
					{
						array[j] = flashData[i + j];
					}
				}
				else
				{
					num2 = num + (uint)array.Length;
					for (int k = 0; k < array.Length; k++)
					{
						array[k] = flashData[i + k];
					}
				}
				flag = sendCmd(243, 65, num, num2 - num, array);
				p?.updateProgress(i / array.Length, STAGE_OTHER);
				if (!flag)
				{
					return false;
				}
			}
			testDevice();
			return true;
		}
		return false;
	}

	public bool programExtFlash(uint start, uint end, uint blockSize, byte[] flashData, IProgress p)
	{
		int num = 0;
		int num2 = 0;
		byte[] array = new byte[blockSize];
		num = flashData.Length;
		while (num > 0)
		{
			long num3 = flashData.Length - num;
			int percent = (int)(num3 * 100 / flashData.Length);
			p?.updateProgress(percent, STAGE_OTHER);
			if (num < blockSize)
			{
				Array.Copy(flashData, num2, array, 0, num);
				if (!sendCmd(243, 64, (uint)num2, (uint)num, array))
				{
					return false;
				}
				num = 0;
				continue;
			}
			num = (int)(num - blockSize);
			Array.Copy(flashData, num2, array, 0L, blockSize);
			if (!sendCmd(243, 64, (uint)num2, blockSize, array))
			{
				return false;
			}
			num2 = (int)(num2 + blockSize);
		}
		testDevice();
		return true;
	}

	public bool readFlash(uint start, uint end, byte[] flashData)
	{
		bool flag = sendCmd(244, 193, start, end - start, flashData);
		if (flag)
		{
			testDevice();
		}
		return flag;
	}

	public byte[] readFlash(uint start, uint end, uint blockSize, IProgress p)
	{
		byte[] array = new byte[end - start];
		byte[] array2 = new byte[blockSize];
		for (uint num = start; num < end; num += blockSize)
		{
			if (end - num < blockSize)
			{
				if (!sendCmd(244, 193, num, end - num, array2))
				{
					return null;
				}
				Array.Copy(array2, 0L, array, num - start, end - num);
			}
			else
			{
				if (!sendCmd(244, 193, num, blockSize, array2))
				{
					return null;
				}
				Array.Copy(array2, 0L, array, num - start, blockSize);
			}
			if (p != null)
			{
				int num2 = 0;
				num2 = (int)(num * 100 / end + 1);
				p.updateProgress(num2, STAGE_OTHER);
			}
		}
		testDevice();
		return array;
	}

	public bool programExtFlash(uint start, uint end, byte[] flashData)
	{
		byte[] lpBuffer = new byte[end - start];
		if (sendCmd(244, 192, start, end, lpBuffer))
		{
			testDevice();
			return true;
		}
		return false;
	}

	public byte[] readExtFlash(uint start, uint end, uint blockSize, IProgress p)
	{
		byte[] array = new byte[end - start];
		byte[] array2 = new byte[blockSize];
		for (uint num = start; num < end; num += blockSize)
		{
			if (end - num < blockSize)
			{
				if (!sendCmd(244, 192, num, end - num, array2))
				{
					return null;
				}
				Array.Copy(array2, 0L, array, num - start, end - num);
			}
			else
			{
				if (!sendCmd(244, 192, num, blockSize, array2))
				{
					return null;
				}
				Array.Copy(array2, 0L, array, num, blockSize);
			}
			if (p != null)
			{
				int num2 = 0;
				long num3 = num;
				num2 = (int)(num3 * 100 / end + 1);
				p.updateProgress(num2, STAGE_OTHER);
			}
		}
		testDevice();
		return array;
	}

	public bool rebootDevice(int mode)
	{
		bool result = false;
		uint num = testDevice();
		if (num != 0 && (num & 2) == 2)
		{
			result = sendCmd(246, 0, (uint)mode, (uint)mode, null);
		}
		return result;
	}

	public uint testDevice()
	{
		byte[] array = new byte[32];
		bool flag = false;
		if (sendCmd(241, 192, 0u, (uint)array.Length, array))
		{
			return BitConverter.ToUInt32(array, 0);
		}
		return 0u;
	}

	public bool isIAPMode()
	{
		uint num = testDevice();
		if (num != 0 && (num & 0x80000000u) == 2147483648u)
		{
			return false;
		}
		return true;
	}

	public bool verify(bool isExtData, uint start_addr, uint length, byte[] data, IProgress p)
	{
		uint num = 65536u;
		byte[] array;
		if (isExtData)
		{
			array = new byte[length];
			bool flag = false;
			for (uint num2 = start_addr; num2 < start_addr + length; num2 += num)
			{
				byte[] array2;
				if (start_addr + length - num2 < num)
				{
					array2 = new byte[start_addr + length - num2];
					if (!sendCmd(244, 192, num2, start_addr + length - num2, array2))
					{
						return false;
					}
					Array.Copy(array2, 0L, array, num2 - start_addr, start_addr + length - num2);
				}
				else
				{
					array2 = new byte[num];
					if (!sendCmd(244, 192, num2, num, array2))
					{
						return false;
					}
					Array.Copy(array2, 0L, array, num2 - start_addr, num);
				}
				for (int i = 0; i < array2.Length; i++)
				{
					if (array2[i] != data[i + num2 - start_addr])
					{
						flag = true;
						break;
					}
				}
				if (p != null)
				{
					int num3 = 0;
					long num4 = num2;
					num3 = (int)(num4 * 100 / (start_addr + length) + 1);
					p.updateProgress(num3, STAGE_OTHER);
				}
				if (flag)
				{
					FileStream fileStream = null;
					FileStream fileStream2 = null;
					try
					{
						fileStream = new FileStream("flash_data.bin", FileMode.Create);
						fileStream2 = new FileStream("read_flash_data.bin", FileMode.Create);
						fileStream.Write(data, 0, data.Length);
						fileStream.Flush();
						fileStream.Close();
						fileStream2.Write(array, 0, array.Length);
						fileStream2.Flush();
						fileStream2.Close();
					}
					catch (Exception)
					{
						fileStream.Close();
						fileStream2.Close();
						fileStream = null;
						fileStream2 = null;
					}
					if (fileStream != null)
					{
						fileStream.Close();
						fileStream = null;
					}
					if (fileStream2 != null)
					{
						fileStream2.Close();
						fileStream2 = null;
					}
					return false;
				}
			}
			testDevice();
			return true;
		}
		array = readFlash(start_addr, start_addr + length, 1024u, p);
		if (array == null)
		{
			return false;
		}
		int num5 = 0;
		for (int j = 0; j < data.Length - 1; j++)
		{
			if (data[j] != array[j])
			{
				num5++;
				break;
			}
		}
		if (num5 > 0)
		{
			FileStream fileStream3 = null;
			FileStream fileStream4 = null;
			try
			{
				fileStream3 = new FileStream("flash_data.bin", FileMode.Create);
				fileStream4 = new FileStream("read_flash_data.bin", FileMode.Create);
				fileStream3.Write(data, 0, data.Length);
				fileStream3.Flush();
				fileStream3.Close();
				fileStream4.Write(array, 0, array.Length);
				fileStream4.Flush();
				fileStream4.Close();
			}
			catch (Exception)
			{
				fileStream3.Close();
				fileStream4.Close();
				fileStream3 = null;
				fileStream4 = null;
			}
			if (fileStream3 != null)
			{
				fileStream3.Close();
				fileStream3 = null;
			}
			if (fileStream4 != null)
			{
				fileStream4.Close();
				fileStream4 = null;
			}
			return false;
		}
		testDevice();
		return true;
	}
}
