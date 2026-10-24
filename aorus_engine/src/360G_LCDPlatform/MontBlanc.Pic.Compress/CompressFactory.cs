using System;
using RLE_Compress;

namespace MontBlanc.Pic.Compress;

internal class CompressFactory
{
	public ICompress createCompress(string type)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected Obj, but got Unknown
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected Obj, but got Unknown
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected Obj, but got Unknown
		if (type.Equals("NONE", StringComparison.OrdinalIgnoreCase))
		{
			return (ICompress)new Compress_NONE();
		}
		if (type.Equals("RLE", StringComparison.OrdinalIgnoreCase))
		{
			return (ICompress)new Compress_RLE();
		}
		if (type.Equals("RLE2", StringComparison.OrdinalIgnoreCase))
		{
			return (ICompress)new Compress_RLE2();
		}
		return null;
	}
}
