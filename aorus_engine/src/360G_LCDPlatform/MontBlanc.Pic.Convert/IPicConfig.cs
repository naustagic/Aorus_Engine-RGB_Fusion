namespace MontBlanc.Pic.Convert;

internal interface IPicConfig
{
	string filename { get; set; }

	string format { get; set; }

	string compress { get; set; }

	string outputType { get; set; }

	bool isHeaderAdd { get; set; }

	bool isMSBFirst { get; set; }

	bool isResizeAll { get; set; }

	int resizeWidth { get; set; }

	int resizeHeight { get; set; }
}
