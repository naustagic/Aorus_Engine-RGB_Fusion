using System;
using System.IO;
using SevenZip;
using SevenZip.Compression.LZMA;

namespace Holtek;

internal class LCDPlatformDataManager : IDataManager
{
	Stream IDataManager.compress(Stream input, ICodeProgress icp)
	{
		long position = input.Position;
		int num = 2097152;
		int num2 = 2;
		int num3 = 3;
		int num4 = 0;
		int num5 = 2;
		int num6 = 128;
		string text = "bt4";
		bool flag = false;
		CoderPropID[] propIDs = new CoderPropID[8]
		{
			CoderPropID.DictionarySize,
			CoderPropID.PosStateBits,
			CoderPropID.LitContextBits,
			CoderPropID.LitPosBits,
			CoderPropID.Algorithm,
			CoderPropID.NumFastBytes,
			CoderPropID.MatchFinder,
			CoderPropID.EndMarker
		};
		object[] properties = new object[8] { num, num2, num3, num4, num5, num6, text, flag };
		Stream stream = new MemoryStream();
		Encoder encoder = new Encoder();
		encoder.SetCoderProperties(propIDs, properties);
		encoder.WriteCoderProperties(stream);
		stream.Write(BitConverter.GetBytes(input.Length), 0, 8);
		encoder.Code(input, stream, -1L, -1L, icp);
		input.Seek(position, SeekOrigin.Begin);
		stream.Seek(0L, SeekOrigin.Begin);
		return stream;
	}

	Stream IDataManager.decompress(Stream input)
	{
		long position = input.Position;
		Stream stream = new MemoryStream();
		Decoder decoder = new Decoder();
		byte[] array = new byte[5];
		input.Read(array, 0, 5);
		byte[] array2 = new byte[8];
		input.Read(array2, 0, 8);
		long outSize = BitConverter.ToInt64(array2, 0);
		decoder.SetDecoderProperties(array);
		decoder.Code(input, stream, input.Length - input.Position, outSize, null);
		input.Seek(position, SeekOrigin.Begin);
		stream.Seek(0L, SeekOrigin.Begin);
		return stream;
	}

	Stream IDataManager.crypto(Stream input, byte[] key)
	{
		long position = input.Position;
		Stream stream = new MemoryStream();
		if (input != null && input.Length > 0 && key != null && key.Length > 0)
		{
			byte b = key[0];
			byte[] array = new byte[input.Length];
			for (long num = 0L; num < input.Length; num++)
			{
				input.Read(array, 0, (int)input.Length);
				array[num] = (byte)(array[num] ^ (b >> (int)(num % 5)));
			}
			stream.Write(array, 0, array.Length);
		}
		input.Seek(position, SeekOrigin.Begin);
		stream.Seek(0L, SeekOrigin.Begin);
		return stream;
	}

	Stream IDataManager.decrypt(Stream input, byte[] key)
	{
		long position = input.Position;
		Stream stream = new MemoryStream();
		if (input != null && input.Length > 0 && key != null && key.Length > 0)
		{
			byte b = key[0];
			byte[] array = new byte[input.Length];
			for (long num = 0L; num < input.Length; num++)
			{
				input.Read(array, 0, (int)input.Length);
				array[num] = (byte)(array[num] ^ (b >> (int)(num % 5)));
			}
			stream.Write(array, 0, array.Length);
		}
		input.Seek(position, SeekOrigin.Begin);
		stream.Seek(0L, SeekOrigin.Begin);
		return stream;
	}
}
