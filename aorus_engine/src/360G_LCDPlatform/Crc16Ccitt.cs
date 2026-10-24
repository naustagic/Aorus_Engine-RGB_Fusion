using System;

public class Crc16Ccitt
{
	private const ushort poly = 4129;

	private ushort[] table = new ushort[256];

	private ushort initialValue = 0;

	public ushort ComputeChecksum(byte[] bytes)
	{
		ushort num = initialValue;
		for (int i = 0; i < bytes.Length; i++)
		{
			num = (ushort)((num << 8) ^ table[(num >> 8) ^ (0xFF & bytes[i])]);
		}
		return num;
	}

	public byte[] ComputeChecksumBytes(byte[] bytes)
	{
		ushort value = ComputeChecksum(bytes);
		return BitConverter.GetBytes(value);
	}

	public Crc16Ccitt(InitialCrcValue initialValue)
	{
		this.initialValue = (ushort)initialValue;
		for (int i = 0; i < table.Length; i++)
		{
			ushort num = 0;
			ushort num2 = (ushort)(i << 8);
			for (int j = 0; j < 8; j++)
			{
				num = ((((num ^ num2) & 0x8000) == 0) ? ((ushort)(num << 1)) : ((ushort)((num << 1) ^ 0x1021)));
				num2 <<= 1;
			}
			table[i] = num;
		}
	}
}
