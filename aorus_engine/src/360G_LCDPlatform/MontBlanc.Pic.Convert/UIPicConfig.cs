namespace MontBlanc.Pic.Convert;

internal class UIPicConfig : IPicConfig
{
	private string filename_;

	private string format_;

	private string compress_;

	private bool isHeaderAdd_;

	private bool isMSBFirst_;

	private bool isResizeAll_;

	private int resizeWidth_;

	private int resizeHeight_;

	private string outputType_;

	public string filename
	{
		get
		{
			return filename_;
		}
		set
		{
			filename_ = value;
		}
	}

	public string format
	{
		get
		{
			return format_;
		}
		set
		{
			format_ = value;
		}
	}

	public string compress
	{
		get
		{
			return compress_;
		}
		set
		{
			compress_ = value;
		}
	}

	public bool isHeaderAdd
	{
		get
		{
			return isHeaderAdd_;
		}
		set
		{
			isHeaderAdd_ = value;
		}
	}

	public bool isMSBFirst
	{
		get
		{
			return isMSBFirst_;
		}
		set
		{
			isMSBFirst_ = value;
		}
	}

	public bool isResizeAll
	{
		get
		{
			return isResizeAll_;
		}
		set
		{
			isResizeAll_ = value;
		}
	}

	public int resizeWidth
	{
		get
		{
			return resizeWidth_;
		}
		set
		{
			resizeWidth_ = value;
		}
	}

	public int resizeHeight
	{
		get
		{
			return resizeHeight_;
		}
		set
		{
			resizeHeight_ = value;
		}
	}

	public string outputType
	{
		get
		{
			return outputType_;
		}
		set
		{
			outputType_ = value;
		}
	}
}
