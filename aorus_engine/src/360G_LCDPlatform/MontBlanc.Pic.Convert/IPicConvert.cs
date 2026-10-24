using System.IO;
using RLE_Compress;

namespace MontBlanc.Pic.Convert;

internal interface IPicConvert
{
	int imageConvertedSize { get; set; }

	int imageWidth { get; set; }

	int imageHeight { get; set; }

	ushort imageFormat { get; set; }

	bool setPicConfig(IPicConfig config);

	Stream getConvertedData(ICompress icp);
}
