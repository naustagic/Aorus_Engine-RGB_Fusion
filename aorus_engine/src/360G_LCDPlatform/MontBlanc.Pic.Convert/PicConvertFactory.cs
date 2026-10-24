using System;

namespace MontBlanc.Pic.Convert;

internal class PicConvertFactory
{
	public IPicConvert createPicConvert(string type)
	{
		// Universal image processor supports BMP, JPG, JPEG, PNG, GIF, WEBP, etc.
		return new BmpConvert();
	}
}
