#define DEBUG
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MontBlanc.Pic.Compress;
using MontBlanc.Pic.Convert;
using MontBlanc.Resources;
using MontBlanc.device;
using RLE_Compress;

namespace MontBlanc;

public class Form1 : Form, IProgress
{
	private delegate bool UpdateInfoTextEventHandler(string str);

	private delegate bool UpdateInformationEventHandler(string str);

	public Thread wt;

	public uint m_hDevice = 0u;

	private Form2 frm2 = null;

	private Form5 frm5;

	public string m_sFilePath;

	public uint m_dwFileSize = 0u;

	public uint m_DataX_len = 0u;

	public uint m_bDbg = 0u;

	public uint m_bIsResize = 0u;

	public uint m_bIsHeader = 1u;

	public uint m_bIsEndian = 1u;

	public ushort m_iBMPType = 1;

	public ushort m_iFileType = 0;

	public uint m_dwResize_Width = 0u;

	public uint m_dwResize_Height = 0u;

	public uint m_bIsToolTipExist = 0u;

	public uint m_bIsEntryAP = 0u;

	public uint m_bIsDownload = 0u;

	public uint m_bIsOneKeyProgram = 0u;

	public uint m_bIsAutoConnectProgram = 0u;

	public uint m_bIsProgramFinishCloseUI = 0u;

	public uint m_bIsSaveBinPath = 0u;

	public string m_sSaveBinPath = "";

	public string m_sDevName = "";

	public ushort m_wVID;

	public ushort m_wPID;

	private bool isUpdating = false;

	public Dictionary<string, string> maps = new Dictionary<string, string>();

	private ToolTip toolTip1 = new ToolTip();

	private Brush m_brush;

	public string m_tNewVersion = "1.0.0";

	public uint m_bIsDownloadAP = 1u;

	private string m_sAPFilePath;

	private string progressLable = "";

	private Dictionary<string, bool> controls_status = new Dictionary<string, bool>();

	private IContainer components = null;

	private Button m_ctlDownload;

	private Label m_labelDevInfo;

	private Button m_ctlSaveImageBin;

	private PictureBox m_ctlPictureBox1;

	private Label m_labelImageInfo;

	private Button m_ctlDemoTool;

	private Label m_lVersion;

	private Button m_bCreateExe;

	private Label label_version;

	private Button bConnect;

	private ProgressBar m_ctlProgressBar1;

	private ListBox m_ctlImage;

	private Button m_ctlAddImage;

	private Button m_ctlDelImage;

	private Button m_ctlOption;

	private Button m_ctlMoveUp;

	private Button m_ctlMoveDown;

	private GroupBox groupBox1;

	private Label LInformation;

	private long start;

	private long stop;

	private long frequency;

	private decimal multiplier = new decimal(1000000000.0);

	public byte[] readBytes = null;

	public byte[] rawBuffer;

	public byte[] m_pCodData;

	public uint m_dwCodLen = 0u;

	private Thread thread_usb = null;

	[DllImport("shell32.dll")]
	public static extern long ShellExecute(int hwnd, string cmd, string file, string param1, string param2, int swmode);

	public Form1()
	{
		Debug.WriteLine("Form1 constructor start");
		InitializeComponent();
		Debug.WriteLine("Form1 InitializeComponent start");
		Init();
		Debug.WriteLine("Form1 Init start");
		m_wVID = 1241;
		m_wPID = 12320;
		Debug.WriteLine("Form1 constructor end");
		AddCreditsPanel();
	}

	/// <summary>Adiciona faixa de créditos no rodapé da janela.</summary>
	private void AddCreditsPanel()
	{
		var pnl = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 26,
			BackColor = Color.FromArgb(18, 18, 20)
		};
		var lbl = new Label
		{
			Text = "by SaraLapia  •  github.com/naustagic/Aorus_Engine-RGB_Fusion",
			Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
			ForeColor = Color.FromArgb(120, 120, 130),
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleCenter,
			Cursor = Cursors.Hand
		};
		lbl.Click += (_, _) =>
		{
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
			{
				FileName = "https://github.com/naustagic/Aorus_Engine-RGB_Fusion",
				UseShellExecute = true
			});
		};
		pnl.Controls.Add(lbl);
		this.Controls.Add(pnl);
	}

	public void Init()
	{
		OpenConfigFile();
		InitThread();
		InitSystem();
	}

	public void InitSystem()
	{
		toolTip1.AutoPopDelay = 2000;
		toolTip1.InitialDelay = 1000;
		toolTip1.ReshowDelay = 500;
		toolTip1.IsBalloon = true;
		toolTip1.ShowAlways = false;
		frm5 = new Form5(this);
	}

	private void IDB_TEST2_Click(object sender, EventArgs e)
	{
	}

	private bool getParameter()
	{
		byte[] array = new byte[1048576];
		Array.Clear(array, 0, array.Length);
		frm5 = new Form5(this);
		DialogResult dialogResult = frm5.ShowDialog();
		if (dialogResult == DialogResult.OK)
		{
			Debug.WriteLine("form5 ok ");
			PUTMSG("OK");
			if (m_bIsDownload == 1)
			{
				if (m_iFileType == 1)
				{
					Debug.WriteLine("image byte is bin");
					OpenFileDialog openFileDialog = new OpenFileDialog();
					openFileDialog.Filter = "Binary (*.BIN)|*.BIN|All files (*.*)|*.*";
					openFileDialog.InitialDirectory = Directory.GetCurrentDirectory();
					openFileDialog.FileName = "Image.bin";
					openFileDialog.Title = "Image Browser";
					if (openFileDialog.ShowDialog() != DialogResult.OK)
					{
						return false;
					}
					string fileName = openFileDialog.FileName;
					m_sFilePath = openFileDialog.FileName;
					m_DataX_len = GetFileSize(fileName);
					m_pCodData = new byte[m_DataX_len];
					LoadBinaryFile(fileName, m_pCodData, m_DataX_len);
				}
				else
				{
					Debug.WriteLine("image byte is file");
					if (!m_labelDevInfo.Text.Equals(Lable.USB_connected) && m_ctlImage.Items.Count > 0)
					{
						Debug.WriteLine("USB is disconnected and build image data");
						if (BuildData() != 0)
						{
							updateInfoText(Lable.Convet_Fault);
							return false;
						}
					}
				}
			}
			if (m_bIsDownloadAP == 1)
			{
				Debug.WriteLine("ap bin has been selected");
				OpenFileDialog openFileDialog = new OpenFileDialog();
				openFileDialog.Filter = "Binary (*.BIN)|*.BIN|All files (*.*)|*.*";
				if (m_sAPFilePath != null && m_sAPFilePath.Length > 0)
				{
					openFileDialog.InitialDirectory = m_sAPFilePath;
				}
				else
				{
					openFileDialog.InitialDirectory = Directory.GetCurrentDirectory();
				}
				openFileDialog.FileName = "AP.bin";
				openFileDialog.Title = "AP Browser";
				if (openFileDialog.ShowDialog() != DialogResult.OK)
				{
					return false;
				}
				m_sAPFilePath = openFileDialog.FileName;
			}
			return true;
		}
		Debug.WriteLine("form5 cancel ");
		PUTMSG("Cancel");
		return false;
	}

	private void IDB_TEST4_Click(object sender, EventArgs e)
	{
		Debug.WriteLine("bt4 clicked");
		m_bIsOneKeyProgram = 0u;
		if (getParameter())
		{
			InitThread();
			wt.Name = "Apple";
			wt.Priority = ThreadPriority.Highest;
			setUpdateStatus(status: true);
			wt.Start(this);
			Debug.WriteLine("program thread started");
		}
	}

	public uint FindDevice()
	{
		HT32F hT32F = new HT32F();
		if (hT32F.findDevice())
		{
			Debug.WriteLine("找到一个设备");
			if (!hT32F.isIAPMode())
			{
				Debug.WriteLine("设备处于AP模式");
				bConnect.Enabled = true;
				m_ctlDownload.Enabled = false;
				return 254u;
			}
			m_ctlDownload.Enabled = true;
			bConnect.Enabled = false;
			m_sDevName = hT32F.deviceName;
			hT32F.closeDevice();
			hT32F = null;
			return 0u;
		}
		hT32F.closeDevice();
		hT32F = null;
		bConnect.Enabled = false;
		m_ctlDownload.Enabled = false;
		Debug.WriteLine("未找到设备");
		return 254u;
	}

	private bool updateInfoText(string str)
	{
		if (str != null)
		{
			if (m_labelDevInfo.InvokeRequired)
			{
				UpdateInfoTextEventHandler method = updateInfoText;
				return (bool)Invoke(method, str);
			}
			m_labelDevInfo.Text = str;
			return true;
		}
		return false;
	}

	private bool updateInformation(string str)
	{
		if (str != null)
		{
			if (LInformation.InvokeRequired)
			{
				UpdateInformationEventHandler method = updateInformation;
				return (bool)Invoke(method, str);
			}
			LInformation.Text = str;
			return true;
		}
		return false;
	}

	public void MyRequest1(object data)
	{
		Form1 form = (Form1)data;
		LCDPlatform lCDPlatform = new LCDPlatform();
		if (!lCDPlatform.findDevice())
		{
			return;
		}
		updateInformation("");
		if (m_bIsDownloadAP == 1)
		{
			int num = m_tNewVersion.Length;
			if (num > 5)
			{
				num = 5;
			}
			byte[] array = new byte[5 + num + 2];
			char[] array2 = new char[m_tNewVersion.Length];
			array[0] = 8;
			array[1] = 0;
			array[2] = 0;
			array[3] = 0;
			array[4] = 86;
			array2 = m_tNewVersion.ToCharArray();
			for (int i = 0; i < num; i++)
			{
				array[i + 5] = (byte)array2[i];
			}
			lCDPlatform.versionBinData = array;
			Debug.WriteLine(m_sAPFilePath);
			FileStream input = File.OpenRead(m_sAPFilePath);
			BinaryReader binaryReader = new BinaryReader(input);
			byte[] aPBinData = binaryReader.ReadBytes((int)binaryReader.BaseStream.Length);
			Debug.WriteLine("AP.bin Read Complete.");
			lCDPlatform.APBinData = aPBinData;
		}
		if (m_bIsDownload == 1)
		{
			updateInfoText(Lable.Start_Download);
			Debug.WriteLine("Start Download");
			if (m_iFileType != 1)
			{
				if (m_iFileType != 0)
				{
					MessageBox.Show(Lable.Not_support_file_type);
					setUpdateStatus(status: false);
					return;
				}
				Debug.WriteLine("Build image data..");
				if (form.BuildData() != 0)
				{
					PUTMSG("Convet Fault!");
					updateInfoText(Lable.Convet_Fault);
					setUpdateStatus(status: false);
					return;
				}
			}
			if (m_DataX_len == 0)
			{
				PUTMSG("Please build image first!");
				setUpdateStatus(status: false);
				return;
			}
			if (m_pCodData.Length % 2048 != 0)
			{
				byte[] array3 = new byte[(m_pCodData.Length / 2048 + 1) * 2048];
				for (int i = 0; i < array3.Length; i++)
				{
					array3[i] = byte.MaxValue;
				}
				Array.Copy(m_pCodData, array3, m_pCodData.Length);
				lCDPlatform.imageBinData = array3;
			}
			else
			{
				byte[] array3 = new byte[m_pCodData.Length - 1];
				Array.Copy(m_pCodData, array3, array3.Length);
				lCDPlatform.imageBinData = array3;
			}
		}
		bool flag = lCDPlatform.program(this);
		double totalTime = lCDPlatform.totalTime;
		double apSpeed = lCDPlatform.apSpeed;
		double imageSpeed = lCDPlatform.imageSpeed;
		string text = Lable.TotalTime + ":" + totalTime.ToString("#0.00") + "s.";
		Debug.WriteLine(text);
		if (flag)
		{
			string text2 = "";
			string text3 = "";
			if (lCDPlatform.APBinData != null && lCDPlatform.APBinData.Length > 0)
			{
				text2 = "AP:" + ((double)lCDPlatform.APBinData.Length / 1024.0).ToString("#0.00") + " KB," + apSpeed.ToString("#0.00") + " KB/s.";
				Debug.WriteLine(text2);
			}
			if (lCDPlatform.imageBinData != null && lCDPlatform.imageBinData.Length > 0)
			{
				text3 = ((lCDPlatform.imageBinData.Length >= 1048576) ? ("Image:" + ((double)lCDPlatform.imageBinData.Length / 1024.0 / 1024.0).ToString("#0.00") + " MB,") : ("Image:" + ((double)lCDPlatform.imageBinData.Length / 1024.0).ToString("#0.00") + " KB,"));
				text3 = text3 + imageSpeed.ToString("#0.00") + " KB/s.";
				Debug.WriteLine(text3);
			}
			UpdateProgressBar(Lable.Done, 100);
			string str = text + text2 + text3;
			updateInformation(str);
		}
		else
		{
			UpdateProgressBar(Lable.Download_Fault, 100);
		}
		if (m_bIsEntryAP == 1 && !lCDPlatform.rebootDevice(0))
		{
			setUpdateStatus(status: false);
			return;
		}
		setUpdateStatus(status: false);
		lCDPlatform.closeDevice();
		lCDPlatform = null;
	}

	private void IDB_TEST5_Click(object sender, EventArgs e)
	{
	}

	private void IDB_TEST6_Click(object sender, EventArgs e)
	{
		HT32F hT32F = new HT32F();
		uint num = 20480u;
		uint num2 = 1024u;
		FileStream input = File.OpenRead("d:\\AP.bin");
		BinaryReader binaryReader = new BinaryReader(input);
		byte[] array = binaryReader.ReadBytes((int)binaryReader.BaseStream.Length);
		input = File.OpenRead("d:\\Ver.bin");
		binaryReader = new BinaryReader(input);
		byte[] array2 = binaryReader.ReadBytes((int)binaryReader.BaseStream.Length);
		hT32F.programFlash(num, num + (uint)array2.Length, array2, this);
		hT32F.programFlash(num + num2, num + num2 + (uint)array.Length, array, this);
		hT32F.closeDevice();
		hT32F = null;
	}

	private void IDB_TEST3_Click(object sender, EventArgs e)
	{
	}

	private uint OpenConfigFile()
	{
		string text = Application.StartupPath + Path.DirectorySeparatorChar + "setting.ini";
		if (File.Exists(text))
		{
			StringBuilder stringBuilder = new StringBuilder(256);
			string text2 = null;
			text2 += "USB Device\n\n";
			GetPrivateProfileString("Common", "BmpType", "", stringBuilder, 256u, text);
			text2 = stringBuilder.ToString();
			m_iBMPType = ushort.Parse(text2);
			GetPrivateProfileString("System", "Dbg", "", stringBuilder, 256u, text);
			text2 = stringBuilder.ToString();
			if (text2.Length > 0)
			{
				m_bDbg = uint.Parse(text2);
			}
			GetPrivateProfileString("Common", "Resize", "", stringBuilder, 256u, text);
			text2 = stringBuilder.ToString();
			m_bIsResize = uint.Parse(text2);
			GetPrivateProfileString("Common", "Header", "", stringBuilder, 256u, text);
			text2 = stringBuilder.ToString();
			m_bIsHeader = uint.Parse(text2);
			GetPrivateProfileString("Common", "Endian", "", stringBuilder, 256u, text);
			text2 = stringBuilder.ToString();
			m_bIsEndian = uint.Parse(text2);
			GetPrivateProfileString("Common", "Width", "", stringBuilder, 256u, text);
			text2 = stringBuilder.ToString();
			m_dwResize_Width = uint.Parse(text2);
			GetPrivateProfileString("Common", "Height", "", stringBuilder, 256u, text);
			text2 = stringBuilder.ToString();
			m_dwResize_Height = uint.Parse(text2);
			GetPrivateProfileString("Bin", "SaveBin", "", stringBuilder, 256u, text);
			text2 = stringBuilder.ToString();
			m_bIsSaveBinPath = uint.Parse(text2);
			GetPrivateProfileString("Bin", "Path", "", stringBuilder, 256u, text);
			m_sSaveBinPath = stringBuilder.ToString();
			GetPrivateProfileString("Bin", "APPath", "", stringBuilder, 256u, text);
			m_sAPFilePath = stringBuilder.ToString();
			GetPrivateProfileString("Image", "Num", "", stringBuilder, 256u, text);
			text2 = stringBuilder.ToString();
			int num = int.Parse(text2);
			maps.Clear();
			for (int i = 0; i < num; i++)
			{
				string lpKeyName = $"File{i}";
				GetPrivateProfileString("Image", lpKeyName, "", stringBuilder, 256u, text);
				string text3 = stringBuilder.ToString();
				GetPrivateProfileString("Tmp", lpKeyName, "", stringBuilder, 256u, text);
				string text4 = stringBuilder.ToString();
				if (IsFileExist(text4) != 0)
				{
					maps.Add(text3, text4);
					m_ctlImage.Items.Add(text3);
				}
			}
			GetPrivateProfileString("Config", "Download", "", stringBuilder, 256u, text);
			text2 = stringBuilder.ToString();
			m_bIsDownload = uint.Parse(text2);
			GetPrivateProfileString("Config", "EntryAP", "", stringBuilder, 256u, text);
			text2 = stringBuilder.ToString();
			m_bIsEntryAP = uint.Parse(text2);
			GetPrivateProfileString("Config", "FileType", "", stringBuilder, 256u, text);
			text2 = stringBuilder.ToString();
			m_iFileType = ushort.Parse(text2);
			GetPrivateProfileString("Config", "DownloadAP", "", stringBuilder, 256u, text);
			text2 = stringBuilder.ToString();
			m_bIsDownloadAP = uint.Parse(text2);
			return 0u;
		}
		return 255u;
	}

	private uint SaveConfigFile()
	{
		string lpFileName = Application.StartupPath + Path.DirectorySeparatorChar + "setting.ini";
		StringBuilder stringBuilder = new StringBuilder(256);
		string text = null;
		text = m_bIsResize.ToString();
		WritePrivateProfileString("Common", "Resize", text, lpFileName);
		text = m_dwResize_Width.ToString();
		WritePrivateProfileString("Common", "Width", text, lpFileName);
		text = m_dwResize_Height.ToString();
		WritePrivateProfileString("Common", "Height", text, lpFileName);
		text = m_bIsHeader.ToString();
		WritePrivateProfileString("Common", "Header", text, lpFileName);
		text = m_bIsEndian.ToString();
		WritePrivateProfileString("Common", "Endian", text, lpFileName);
		text = m_iBMPType.ToString();
		WritePrivateProfileString("Common", "BmpType", text, lpFileName);
		text = m_bIsSaveBinPath.ToString();
		WritePrivateProfileString("Bin", "SaveBin", text, lpFileName);
		text = m_sSaveBinPath;
		WritePrivateProfileString("Bin", "Path", text, lpFileName);
		text = m_sAPFilePath;
		WritePrivateProfileString("Bin", "APPath", text, lpFileName);
		text = m_ctlImage.Items.Count.ToString();
		WritePrivateProfileString("Image", "Num", text, lpFileName);
		int count = m_ctlImage.Items.Count;
		for (int i = 0; i < count; i++)
		{
			string lpKeyName = $"File{i}";
			text = m_ctlImage.Items[i].ToString();
			WritePrivateProfileString("Image", lpKeyName, text, lpFileName);
			text = maps[m_ctlImage.Items[i].ToString()];
			WritePrivateProfileString("Tmp", lpKeyName, text, lpFileName);
		}
		text = m_bIsDownload.ToString();
		WritePrivateProfileString("Config", "Download", text, lpFileName);
		text = m_bIsEntryAP.ToString();
		WritePrivateProfileString("Config", "EntryAP", text, lpFileName);
		text = m_iFileType.ToString();
		WritePrivateProfileString("Config", "FileType", text, lpFileName);
		text = m_bIsDownloadAP.ToString();
		WritePrivateProfileString("Config", "DownloadAP", text, lpFileName);
		return 0u;
	}

	private uint GetBinData()
	{
		return 0u;
	}

	private uint BuildData()
	{
		return Build();
	}

	private uint EraseData()
	{
		return 0u;
	}

	public uint WriteData()
	{
		return 0u;
	}

	private uint BitmapCustoms()
	{
		string text = GetAppDir() + "\\tmp";
		int count = m_ctlImage.Items.Count;
		for (int i = 0; i < count; i++)
		{
			string text2 = m_ctlImage.Items[i].ToString();
			string text3 = maps[text2];
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text3);
			string extension = Path.GetExtension(text3);
			string fileNameWithoutExtension2 = Path.GetFileNameWithoutExtension(text2);
			if (extension != ".bmp" && extension != ".BMP")
			{
				switch (extension)
				{
				default:
					if (!(extension == ".TIFF"))
					{
						MessageBox.Show("不支援的檔案類型!");
						return 255u;
					}
					goto case ".jpg";
				case ".jpg":
				case ".JPG":
				case ".png":
				case ".PNG":
				case ".gif":
				case ".GIF":
				case ".tiff":
				{
					Bitmap bitmap = (Bitmap)Image.FromFile(text3);
					string text4;
					if (m_bIsResize == 1)
					{
						Size size = new Size((int)m_dwResize_Width, (int)m_dwResize_Height);
						Bitmap bitmap2 = ResizeImage(bitmap, size);
						bitmap.Dispose();
						int num = bitmap2.Width;
						int num2 = bitmap2.Height;
						text4 = text + "\\" + fileNameWithoutExtension + ".bmp";
						SaveBitmap(bitmap2, text4);
						bitmap2.Dispose();
					}
					else
					{
						int num = bitmap.Width;
						int num2 = bitmap.Height;
						text4 = text + "\\" + fileNameWithoutExtension + ".bmp";
						SaveBitmap(bitmap, text4);
						bitmap.Dispose();
					}
					string key = m_ctlImage.Items[i].ToString();
					maps[key] = text4;
					break;
				}
				}
			}
			else if (m_bIsResize == 1)
			{
				Bitmap bitmap = (Bitmap)Image.FromFile(text3);
				Size size = new Size((int)m_dwResize_Width, (int)m_dwResize_Height);
				Bitmap bitmap2 = ResizeImage(bitmap, size);
				bitmap.Dispose();
				int num = bitmap2.Width;
				int num2 = bitmap2.Height;
				string text4 = text + "\\" + fileNameWithoutExtension + ".bmp";
				SaveBitmap(bitmap2, text4);
				bitmap2.Dispose();
				string key = m_ctlImage.Items[i].ToString();
				maps[key] = text4;
			}
		}
		return 0u;
	}

	public string GetNowTime()
	{
		return $"{DateTime.Now.Hour,2:D2}{DateTime.Now.Minute,2:D2}{DateTime.Now.Second,2:D2}";
	}

	private void IDB_TEST1_Click(object sender, EventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Filter = "Images (*.BMP;*.JPG;*.PNG;*.GIF;*.TIFF)|*.BMP;*.JPG;*.PNG;*.GIF;*.TIFF|All files (*.*)|*.*";
		openFileDialog.Title = "Select a Cursor File";
		if (openFileDialog.ShowDialog() == DialogResult.OK)
		{
			Bitmap imgToResize = (Bitmap)Image.FromFile(openFileDialog.FileName);
			Size size = new Size(800, 480);
			imgToResize = ResizeImage(imgToResize, size);
			SaveBitmap(imgToResize, "D:\\16.bmp");
		}
	}

	private void InitThread()
	{
		try
		{
			wt = new Thread(MyRequest1);
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message.ToString());
		}
	}

	private void m_ctlAddImage_Click(object sender, EventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Filter = "Supported Images (*.PNG;*.JPG;*.JPEG;*.BMP;*.GIF;*.WEBP)|*.PNG;*.JPG;*.JPEG;*.BMP;*.GIF;*.WEBP|All files (*.*)|*.*";
		openFileDialog.Multiselect = true;
		openFileDialog.Title = "My Image Browser";
		openFileDialog.ShowDialog();
		string[] fileNames = openFileDialog.FileNames;
		string[] array = fileNames;
		foreach (string text in array)
		{
			string fileName = Path.GetFileName(text);
			int num = m_ctlImage.FindString(fileName);
			if (num == -1)
			{
				m_ctlImage.Items.Add(fileName);
				maps.Add(fileName, text);
			}
		}
	}

	private void m_ctlDelImage_Click(object sender, EventArgs e)
	{
		int selectedIndex = m_ctlImage.SelectedIndex;
		if (selectedIndex != -1 && m_ctlImage.Items.Count != 0)
		{
			m_ctlPictureBox1.Visible = false;
			m_labelImageInfo.Visible = false;
			string key = m_ctlImage.Items[selectedIndex].ToString();
			m_ctlImage.Items.RemoveAt(selectedIndex);
			maps.Remove(key);
			if (m_ctlImage.Items.Count > 0)
			{
				selectedIndex = ((selectedIndex - 1 >= 0) ? (selectedIndex - 1) : 0);
				m_ctlImage.SetSelected(selectedIndex, value: true);
			}
		}
	}

	private void Form1_FormClosed(object sender, FormClosedEventArgs e)
	{
		wt.Abort();
		SaveConfigFile();
	}

	private void UpdateProgressBar(string sDesc, int i)
	{
		if (m_ctlProgressBar1.InvokeRequired)
		{
			if (i > 100)
			{
				i = 100;
			}
			if (i < 0)
			{
				i = 0;
			}
			m_ctlProgressBar1.Invoke(new Action<string, int>(UpdateProgressBar), sDesc, i);
		}
		else
		{
			if (i > 50)
			{
				m_brush = Brushes.Black;
			}
			else
			{
				m_brush = Brushes.DarkGreen;
			}
			m_ctlProgressBar1.Value = i;
			m_ctlProgressBar1.Refresh();
			m_labelDevInfo.Text = sDesc;
		}
	}

	private void PUTMSG(string msg)
	{
	}

	private void m_ctlOption_Click(object sender, EventArgs e)
	{
		if (frm2 == null)
		{
			frm2 = new Form2(this);
		}
		frm2.ShowDialog();
	}

	private void m_ctlImage_DoubleClick(object sender, EventArgs e)
	{
		ListBox listBox = (ListBox)sender;
		int selectedIndex = listBox.SelectedIndex;
		if (selectedIndex != -1)
		{
			string text = m_ctlImage.Items[selectedIndex].ToString();
		}
	}

	private void Form1_Load(object sender, EventArgs e)
	{
		Debug.WriteLine("Form1 Load start");
		m_labelDevInfo.Text = Lable.USB_disconnected;
		if (FindDevice() == 0)
		{
			m_labelDevInfo.Text = Lable.USB_connected;
			getVersion();
		}
		ToolTip toolTip = new ToolTip();
		toolTip.AutoPopDelay = 10000;
		toolTip.InitialDelay = 1000;
		toolTip.ReshowDelay = 500;
		toolTip.IsBalloon = true;
		toolTip.ShowAlways = true;
		toolTip.SetToolTip(m_bCreateExe, Lable.Create_exe);
		toolTip.SetToolTip(m_lVersion, Lable.Version);
		toolTip.SetToolTip(label_version, "Clique para verificar novas versões no GitHub");
		toolTip.SetToolTip(m_lVersion, "Clique para verificar novas versões no GitHub");
		toolTip.SetToolTip(m_ctlDownload, Lable.btn_download_tips);
		toolTip.SetToolTip(m_ctlAddImage, "");
		toolTip.SetToolTip(m_ctlDelImage, "");
		toolTip.SetToolTip(m_ctlMoveUp, "");
		toolTip.SetToolTip(m_ctlMoveDown, "");
		toolTip.SetToolTip(m_ctlOption, "");
		toolTip.SetToolTip(m_ctlDemoTool, "");

		label_version.Cursor = Cursors.Hand;
		label_version.Click += Label_version_Click;
		m_lVersion.Cursor = Cursors.Hand;
		m_lVersion.Click += Label_version_Click;
	}

	private async void Label_version_Click(object sender, EventArgs e)
	{
		string repo = PromptInput(
			"Digite o repositório GitHub (ex: seu-usuario/seu-repositorio):",
			"Verificador de Versão GitHub",
			"naustagic/Aorus_Engine-RGB_Fusion");

		if (string.IsNullOrWhiteSpace(repo)) return;

		try
		{
			var release = await AorusCoolerConverter.Core.AorusImageProcessor.CheckForGitHubUpdatesAsync(repo, "v2.3.1");
			if (release.HasUpdate)
			{
				var dr = MessageBox.Show(
					$"Nova versão disponível no GitHub!\nVersão: {release.TagName}\n\nNotas:\n{release.Body}\n\nDeseja abrir a página de download no navegador?",
					"Atualização Encontrada",
					MessageBoxButtons.YesNo,
					MessageBoxIcon.Information);

				if (dr == DialogResult.Yes && !string.IsNullOrEmpty(release.HtmlUrl))
				{
					System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
					{
						FileName = release.HtmlUrl,
						UseShellExecute = true
					});
				}
			}
			else
			{
				MessageBox.Show($"Você está utilizando a versão mais recente ({release.TagName}).", "Atualizações", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Não foi possível verificar atualizações: " + ex.Message, "Verificação de Atualização", MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}
	}

	private static string PromptInput(string promptText, string title, string defaultVal)
	{
		Form prompt = new Form()
		{
			Width = 460,
			Height = 165,
			FormBorderStyle = FormBorderStyle.FixedDialog,
			Text = title,
			StartPosition = FormStartPosition.CenterScreen,
			MaximizeBox = false,
			MinimizeBox = false
		};
		Label lbl = new Label() { Left = 20, Top = 15, Text = promptText, AutoSize = true };
		TextBox txt = new TextBox() { Left = 20, Top = 40, Width = 400, Text = defaultVal };
		Button btnOk = new Button() { Text = "Verificar", Left = 230, Width = 90, Top = 80, DialogResult = DialogResult.OK };
		Button btnCancel = new Button() { Text = "Cancelar", Left = 330, Width = 90, Top = 80, DialogResult = DialogResult.Cancel };
		btnOk.Click += (s, ev) => prompt.Close();
		btnCancel.Click += (s, ev) => prompt.Close();
		prompt.Controls.Add(lbl);
		prompt.Controls.Add(txt);
		prompt.Controls.Add(btnOk);
		prompt.Controls.Add(btnCancel);
		prompt.AcceptButton = btnOk;
		prompt.CancelButton = btnCancel;
		return prompt.ShowDialog() == DialogResult.OK ? txt.Text.Trim() : "";
	}

	private void m_ctlMoveUp_Click(object sender, EventArgs e)
	{
		int selectedIndex = m_ctlImage.SelectedIndex;
		if (selectedIndex != 0 && selectedIndex != -1)
		{
			string text = m_ctlImage.Items[selectedIndex].ToString();
			string text2 = maps[text];
			m_ctlImage.Items.RemoveAt(selectedIndex);
			m_ctlImage.Items.Insert(selectedIndex - 1, text);
			m_ctlImage.SetSelected(selectedIndex - 1, value: true);
		}
	}

	private void m_ctlMoveDown_Click(object sender, EventArgs e)
	{
		int selectedIndex = m_ctlImage.SelectedIndex;
		int count = m_ctlImage.Items.Count;
		if (selectedIndex != count - 1 && selectedIndex != -1)
		{
			string text = m_ctlImage.Items[selectedIndex].ToString();
			string text2 = maps[text];
			m_ctlImage.Items.RemoveAt(selectedIndex);
			m_ctlImage.Items.Insert(selectedIndex + 1, text);
			m_ctlImage.SetSelected(selectedIndex + 1, value: true);
		}
	}

	private void m_ctlImage_KeyDown(object sender, KeyEventArgs e)
	{
		int num = 0;
		if (e.KeyCode != Keys.Delete)
		{
			return;
		}
		num = m_ctlImage.SelectedIndex;
		if (num != -1)
		{
			m_ctlPictureBox1.Visible = false;
			m_labelImageInfo.Visible = false;
			string key = m_ctlImage.Items[num].ToString();
			m_ctlImage.Items.RemoveAt(num);
			maps.Remove(key);
			if (m_ctlImage.Items.Count > 0)
			{
				num = ((num - 1 >= 0) ? (num - 1) : 0);
				m_ctlImage.SetSelected(num, value: true);
			}
		}
	}

	private void Form1_MouseMove(object sender, MouseEventArgs e)
	{
		if (m_bIsToolTipExist == 1)
		{
			m_bIsToolTipExist = 0u;
			toolTip1.Hide(this);
		}
	}

	private void Form1_Paint(object sender, PaintEventArgs e)
	{
	}

	private void m_ctlImage_Click(object sender, EventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog();
		int selectedIndex = m_ctlImage.SelectedIndex;
		if (selectedIndex == -1)
		{
			return;
		}
		m_ctlPictureBox1.Visible = true;
		m_labelImageInfo.Visible = true;
		string text = m_ctlImage.Items[selectedIndex].ToString();
		string filename = maps[text];
		Bitmap bitmap = (Bitmap)Image.FromFile(filename);
		if (m_ctlPictureBox1.Image != null)
		{
			m_ctlPictureBox1.Image.Dispose();
		}
		int num = m_ctlPictureBox1.Width;
		int num2 = m_ctlPictureBox1.Height;
		int num3 = bitmap.Width;
		int num4 = bitmap.Height;
		int num5 = 0;
		for (num5 = 100; num5 > 0; num5--)
		{
			float num6 = 0.1f * (float)num5;
			float num7 = (float)num3 * num6;
			float num8 = (float)num4 * num6;
			if (!(num7 > (float)num) && !(num8 > (float)num2))
			{
				num3 = (int)num7;
				num4 = (int)num8;
				break;
			}
		}
		Size size = new Size(num3, num4);
		Bitmap image = ResizeImage(bitmap, size);
		string text2 = $"{text} ({bitmap.Width} x {bitmap.Height})";
		m_labelImageInfo.Text = text2;
		bitmap.Dispose();
		m_ctlPictureBox1.Image = image;
	}

	private void m_ctlSaveImageBin_Click(object sender, EventArgs e)
	{
		SaveFileDialog saveFileDialog = new SaveFileDialog();
		saveFileDialog.Filter = "BIN files (*.bin)|*.bin|All files (*.*)|*.*";
		saveFileDialog.FilterIndex = 1;
		saveFileDialog.RestoreDirectory = true;
		saveFileDialog.FileName = "Image3.bin";
		saveFileDialog.Title = "Image Browser";
		if (m_ctlImage.Items.Count != 0 && saveFileDialog.ShowDialog() == DialogResult.OK)
		{
			m_sFilePath = saveFileDialog.FileName;
			BuildData();
		}
	}

	private void m_ctlLoadImageBin_Click(object sender, EventArgs e)
	{
	}

	private void m_ctlImage_SelectedIndexChanged(object sender, EventArgs e)
	{
	}

	private void m_ctlDemoTool_Click(object sender, EventArgs e)
	{
		ShellExecute(0, "open", "UartTest.exe", "", "", 5);
		Process process = new Process();
		string fileName = GetAppDir() + "\\CommTest.exe";
		process.StartInfo.FileName = fileName;
		process.Start();
	}

	private void m_lVersion_MouseHover(object sender, EventArgs e)
	{
	}

	private void generateExe(object data)
	{
		if (!getParameter())
		{
			return;
		}
		bool updateStatus = isUpdating;
		setUpdateStatus(status: true);
		UpdateProgressBar("", 0);
		Writer writer = new Writer();
		string executablePath = Application.ExecutablePath;
		writer.addExeFile(executablePath);
		string sFilePath = m_sFilePath;
		string sAPFilePath = m_sAPFilePath;
		string text = GetAppDir() + "\\AP";
		string text2 = text + "\\AP.exe";
		DirectoryInfo directoryInfo = new DirectoryInfo(GetAppDir());
		directoryInfo.CreateSubdirectory("AP");
		if (m_bIsDownload == 1)
		{
			writer.addAdditionalFile(sFilePath);
		}
		else
		{
			writer.addAdditionalFile((string)null);
		}
		if (m_bIsDownloadAP == 1)
		{
			writer.addAdditionalFile(sAPFilePath);
		}
		else
		{
			writer.addAdditionalFile((string)null);
		}
		int num = m_tNewVersion.Length;
		if (num > 5)
		{
			num = 5;
		}
		byte[] array = new byte[5 + num + 2];
		char[] array2 = new char[m_tNewVersion.Length];
		array[0] = 8;
		array[1] = 0;
		array[2] = 0;
		array[3] = 0;
		array[4] = 86;
		array2 = m_tNewVersion.ToCharArray();
		for (int i = 0; i < num; i++)
		{
			array[i + 5] = (byte)array2[i];
		}
		writer.addAdditionalFile(array);
		byte[] array3 = new byte[1] { 0 };
		if (m_bIsAutoConnectProgram == 1)
		{
			array3[0] |= 1;
		}
		if (m_bIsProgramFinishCloseUI == 1)
		{
			array3[0] |= 2;
		}
		writer.addAdditionalFile(array3);
		if (!writer.output(text2, this))
		{
			showMessage(Lable.FileOpen + "\r\n" + text2, "Fault");
			return;
		}
		DirectoryInfo directoryInfo2 = new DirectoryInfo(GetAppDir() + "\\AP\\zh-CN");
		FileInfo fileInfo = new FileInfo(GetAppDir() + "\\AP\\zh-CN\\LCD_Factory.resources.dll");
		DirectoryInfo directoryInfo3 = new DirectoryInfo(GetAppDir() + "\\zh-CN");
		FileInfo fileInfo2 = new FileInfo(GetAppDir() + "\\zh-CN\\LCD_Factory.resources.dll");
		if (directoryInfo3.Exists && fileInfo2.Exists)
		{
			if (!directoryInfo2.Exists)
			{
				directoryInfo2.Create();
				File.Copy(GetAppDir() + "\\zh-CN\\LCD_Factory.resources.dll", fileInfo.FullName);
			}
			else if (!fileInfo.Exists)
			{
				File.Copy(GetAppDir() + "\\zh-CN\\LCD_Factory.resources.dll", fileInfo.FullName);
			}
		}
		DirectoryInfo directoryInfo4 = new DirectoryInfo(GetAppDir() + "\\AP\\zh-TW");
		FileInfo fileInfo3 = new FileInfo(GetAppDir() + "\\AP\\zh-TW\\LCD_Factory.resources.dll");
		DirectoryInfo directoryInfo5 = new DirectoryInfo(GetAppDir() + "\\zh-TW");
		FileInfo fileInfo4 = new FileInfo(GetAppDir() + "\\zh-TW\\LCD_Factory.resources.dll");
		if (directoryInfo5.Exists && fileInfo4.Exists)
		{
			if (!directoryInfo2.Exists)
			{
				directoryInfo4.Create();
				File.Copy(GetAppDir() + "\\zh-TW\\LCD_Factory.resources.dll", fileInfo3.FullName);
			}
			else if (!fileInfo3.Exists)
			{
				File.Copy(GetAppDir() + "\\zh-TW\\LCD_Factory.resources.dll", fileInfo3.FullName);
			}
		}
		UpdateProgressBar(Lable.Done, 100);
		showMessage(text2, "File");
		setUpdateStatus(updateStatus);
	}

	private void m_bCreateExe_Click(object sender, EventArgs e)
	{
		Thread thread = new Thread(generateExe);
		thread.SetApartmentState(ApartmentState.STA);
		m_bIsOneKeyProgram = 1u;
		thread.Start();
	}

	private void m_labelDevInfo_Click(object sender, EventArgs e)
	{
	}

	bool IProgress.updateProgress(int percent, int stage)
	{
		switch (stage)
		{
		case 1:
			progressLable = Lable.Erase_Version;
			break;
		case 2:
			progressLable = Lable.Programing_Version;
			break;
		case 3:
			progressLable = Lable.Verify_Done;
			break;
		case 4:
			progressLable = Lable.Erase_and_program_ap;
			break;
		case 5:
			progressLable = Lable.Erase_and_program_ap;
			break;
		case 6:
			progressLable = Lable.Verify_AP;
			break;
		case 7:
			progressLable = Lable.Erase_Image;
			break;
		case 8:
			progressLable = Lable.Download_Image;
			break;
		case 9:
			progressLable = Lable.Verify_Image;
			break;
		}
		UpdateProgressBar(progressLable, percent);
		return true;
	}

	bool IProgress.setLable(string info)
	{
		progressLable = info;
		return true;
	}

	private void Form1_FormClosing(object sender, FormClosingEventArgs e)
	{
		if (isUpdating)
		{
			e.Cancel = true;
		}
		if (thread_usb != null && thread_usb.IsAlive)
		{
			thread_usb.Abort();
		}
		HT32F hT32F = new HT32F();
		if (hT32F.findDevice() && hT32F.isIAPMode())
		{
			hT32F.rebootDevice(0);
		}
	}

	private void setUpdateStatus(bool status)
	{
		Debug.WriteLine("UpdateStatus......");
		foreach (Control control in Controls)
		{
			if (!(control is Button))
			{
				continue;
			}
			if (control.InvokeRequired)
			{
				control.Invoke(new Action<bool>(setUpdateStatus), status);
			}
			else if (status)
			{
				if (!controls_status.ContainsKey(control.Text))
				{
					controls_status.Add(control.Text, control.Enabled);
				}
				control.Enabled = false;
			}
			else if (controls_status.Count != 0)
			{
				bool value = false;
				controls_status.TryGetValue(control.Text, out value);
				control.Enabled = value;
				controls_status.Remove(control.Text);
			}
		}
		if (!status)
		{
		}
		isUpdating = status;
	}

	private void bConnect_Click(object sender, EventArgs e)
	{
		HT32F hT32F = new HT32F();
		if (hT32F.findDevice() && !hT32F.isIAPMode())
		{
			Debug.WriteLine("复位设备到IAP中");
			hT32F.rebootDevice(1);
		}
		hT32F.closeDevice();
		hT32F = null;
	}

	private void showMessage(string text, string caption)
	{
		if (InvokeRequired)
		{
			Invoke(new Action<string, string>(showMessage), text, caption);
		}
		else
		{
			MessageBox.Show(text, caption);
		}
	}

	private void m_ctlDelImage_MouseClick(object sender, MouseEventArgs e)
	{
	}

	private void m_ctlDelImage_MouseDown(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Right)
		{
			maps.Clear();
			m_ctlImage.Items.Clear();
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MontBlanc.Form1));
		this.m_ctlDownload = new System.Windows.Forms.Button();
		this.m_labelDevInfo = new System.Windows.Forms.Label();
		this.m_ctlSaveImageBin = new System.Windows.Forms.Button();
		this.m_ctlPictureBox1 = new System.Windows.Forms.PictureBox();
		this.m_labelImageInfo = new System.Windows.Forms.Label();
		this.m_ctlDemoTool = new System.Windows.Forms.Button();
		this.m_lVersion = new System.Windows.Forms.Label();
		this.m_bCreateExe = new System.Windows.Forms.Button();
		this.label_version = new System.Windows.Forms.Label();
		this.bConnect = new System.Windows.Forms.Button();
		this.m_ctlProgressBar1 = new System.Windows.Forms.ProgressBar();
		this.m_ctlImage = new System.Windows.Forms.ListBox();
		this.m_ctlAddImage = new System.Windows.Forms.Button();
		this.m_ctlDelImage = new System.Windows.Forms.Button();
		this.m_ctlOption = new System.Windows.Forms.Button();
		this.m_ctlMoveUp = new System.Windows.Forms.Button();
		this.m_ctlMoveDown = new System.Windows.Forms.Button();
		this.groupBox1 = new System.Windows.Forms.GroupBox();
		this.LInformation = new System.Windows.Forms.Label();
		((System.ComponentModel.ISupportInitialize)this.m_ctlPictureBox1).BeginInit();
		this.groupBox1.SuspendLayout();
		base.SuspendLayout();
		resources.ApplyResources(this.m_ctlDownload, "m_ctlDownload");
		this.m_ctlDownload.Name = "m_ctlDownload";
		this.m_ctlDownload.UseVisualStyleBackColor = true;
		this.m_ctlDownload.Click += new System.EventHandler(this.IDB_TEST4_Click);
		resources.ApplyResources(this.m_labelDevInfo, "m_labelDevInfo");
		this.m_labelDevInfo.Name = "m_labelDevInfo";
		this.m_labelDevInfo.Click += new System.EventHandler(this.m_labelDevInfo_Click);
		resources.ApplyResources(this.m_ctlSaveImageBin, "m_ctlSaveImageBin");
		this.m_ctlSaveImageBin.Name = "m_ctlSaveImageBin";
		this.m_ctlSaveImageBin.UseVisualStyleBackColor = true;
		this.m_ctlSaveImageBin.Click += new System.EventHandler(this.m_ctlSaveImageBin_Click);
		this.m_ctlPictureBox1.BackColor = System.Drawing.SystemColors.Desktop;
		resources.ApplyResources(this.m_ctlPictureBox1, "m_ctlPictureBox1");
		this.m_ctlPictureBox1.Name = "m_ctlPictureBox1";
		this.m_ctlPictureBox1.TabStop = false;
		this.m_labelImageInfo.BackColor = System.Drawing.SystemColors.Desktop;
		resources.ApplyResources(this.m_labelImageInfo, "m_labelImageInfo");
		this.m_labelImageInfo.ForeColor = System.Drawing.SystemColors.Window;
		this.m_labelImageInfo.Name = "m_labelImageInfo";
		resources.ApplyResources(this.m_ctlDemoTool, "m_ctlDemoTool");
		this.m_ctlDemoTool.Name = "m_ctlDemoTool";
		this.m_ctlDemoTool.UseVisualStyleBackColor = true;
		this.m_ctlDemoTool.Click += new System.EventHandler(this.m_ctlDemoTool_Click);
		resources.ApplyResources(this.m_lVersion, "m_lVersion");
		this.m_lVersion.ForeColor = System.Drawing.Color.Red;
		this.m_lVersion.Name = "m_lVersion";
		this.m_lVersion.MouseHover += new System.EventHandler(this.m_lVersion_MouseHover);
		resources.ApplyResources(this.m_bCreateExe, "m_bCreateExe");
		this.m_bCreateExe.Name = "m_bCreateExe";
		this.m_bCreateExe.UseVisualStyleBackColor = true;
		this.m_bCreateExe.Click += new System.EventHandler(this.m_bCreateExe_Click);
		resources.ApplyResources(this.label_version, "label_version");
		this.label_version.ForeColor = System.Drawing.Color.Red;
		this.label_version.Name = "label_version";
		resources.ApplyResources(this.bConnect, "bConnect");
		this.bConnect.Name = "bConnect";
		this.bConnect.UseVisualStyleBackColor = true;
		this.bConnect.Click += new System.EventHandler(this.bConnect_Click);
		resources.ApplyResources(this.m_ctlProgressBar1, "m_ctlProgressBar1");
		this.m_ctlProgressBar1.Name = "m_ctlProgressBar1";
		resources.ApplyResources(this.m_ctlImage, "m_ctlImage");
		this.m_ctlImage.FormattingEnabled = true;
		this.m_ctlImage.Name = "m_ctlImage";
		this.m_ctlImage.Click += new System.EventHandler(this.m_ctlImage_Click);
		this.m_ctlImage.SelectedIndexChanged += new System.EventHandler(this.m_ctlImage_SelectedIndexChanged);
		this.m_ctlImage.DoubleClick += new System.EventHandler(this.m_ctlImage_DoubleClick);
		this.m_ctlImage.KeyDown += new System.Windows.Forms.KeyEventHandler(this.m_ctlImage_KeyDown);
		resources.ApplyResources(this.m_ctlAddImage, "m_ctlAddImage");
		this.m_ctlAddImage.Name = "m_ctlAddImage";
		this.m_ctlAddImage.UseVisualStyleBackColor = true;
		this.m_ctlAddImage.Click += new System.EventHandler(this.m_ctlAddImage_Click);
		resources.ApplyResources(this.m_ctlDelImage, "m_ctlDelImage");
		this.m_ctlDelImage.Name = "m_ctlDelImage";
		this.m_ctlDelImage.UseVisualStyleBackColor = true;
		this.m_ctlDelImage.Click += new System.EventHandler(this.m_ctlDelImage_Click);
		this.m_ctlDelImage.MouseClick += new System.Windows.Forms.MouseEventHandler(this.m_ctlDelImage_MouseClick);
		this.m_ctlDelImage.MouseDown += new System.Windows.Forms.MouseEventHandler(this.m_ctlDelImage_MouseDown);
		resources.ApplyResources(this.m_ctlOption, "m_ctlOption");
		this.m_ctlOption.Name = "m_ctlOption";
		this.m_ctlOption.UseVisualStyleBackColor = true;
		this.m_ctlOption.Click += new System.EventHandler(this.m_ctlOption_Click);
		resources.ApplyResources(this.m_ctlMoveUp, "m_ctlMoveUp");
		this.m_ctlMoveUp.Name = "m_ctlMoveUp";
		this.m_ctlMoveUp.UseVisualStyleBackColor = true;
		this.m_ctlMoveUp.Click += new System.EventHandler(this.m_ctlMoveUp_Click);
		resources.ApplyResources(this.m_ctlMoveDown, "m_ctlMoveDown");
		this.m_ctlMoveDown.Name = "m_ctlMoveDown";
		this.m_ctlMoveDown.UseVisualStyleBackColor = true;
		this.m_ctlMoveDown.Click += new System.EventHandler(this.m_ctlMoveDown_Click);
		this.groupBox1.Controls.Add(this.m_labelImageInfo);
		this.groupBox1.Controls.Add(this.m_ctlPictureBox1);
		this.groupBox1.Controls.Add(this.m_ctlMoveDown);
		this.groupBox1.Controls.Add(this.m_ctlMoveUp);
		this.groupBox1.Controls.Add(this.m_ctlOption);
		this.groupBox1.Controls.Add(this.m_ctlDelImage);
		this.groupBox1.Controls.Add(this.m_ctlAddImage);
		this.groupBox1.Controls.Add(this.m_ctlImage);
		resources.ApplyResources(this.groupBox1, "groupBox1");
		this.groupBox1.Name = "groupBox1";
		this.groupBox1.TabStop = false;
		resources.ApplyResources(this.LInformation, "LInformation");
		this.LInformation.Name = "LInformation";
		resources.ApplyResources(this, "$this");
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.SystemColors.Control;
		base.Controls.Add(this.LInformation);
		base.Controls.Add(this.groupBox1);
		base.Controls.Add(this.m_ctlProgressBar1);
		base.Controls.Add(this.bConnect);
		base.Controls.Add(this.label_version);
		base.Controls.Add(this.m_bCreateExe);
		base.Controls.Add(this.m_lVersion);
		base.Controls.Add(this.m_ctlDemoTool);
		base.Controls.Add(this.m_ctlSaveImageBin);
		base.Controls.Add(this.m_labelDevInfo);
		base.Controls.Add(this.m_ctlDownload);
		this.ForeColor = System.Drawing.SystemColors.ControlText;
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		base.MaximizeBox = false;
		base.Name = "Form1";
		base.TransparencyKey = System.Drawing.Color.FromArgb(192, 192, 255);
		base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
		base.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
		base.Load += new System.EventHandler(this.Form1_Load);
		base.Paint += new System.Windows.Forms.PaintEventHandler(this.Form1_Paint);
		base.MouseMove += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseMove);
		((System.ComponentModel.ISupportInitialize)this.m_ctlPictureBox1).EndInit();
		this.groupBox1.ResumeLayout(false);
		base.ResumeLayout(false);
		base.PerformLayout();
	}

	[DllImport("kernel32.dll")]
	private static extern int GetPrivateProfileSectionNames(IntPtr lpszReturnBuffer, uint nSize, string lpFileName);

	[DllImport("kernel32.dll")]
	private static extern uint GetPrivateProfileString(string lpAppName, string lpKeyName, string lpDefault, StringBuilder lpReturnedString, uint nSize, string lpFileName);

	[DllImport("kernel32.dll")]
	private static extern uint WritePrivateProfileString(string lpAppName, string lpKeyName, string lpVal, string lpFileName);

	[DllImport("kernel32.dll")]
	private static extern uint GetPrivateProfileInt(string lpAppName, string lpKeyName, int nDefault, string lpFileName);

	[DllImport("KERNEL32")]
	private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

	[DllImport("Kernel32.dll")]
	private static extern bool QueryPerformanceFrequency(out long lpFrequency);

	[DllImport("kernel32.dll")]
	private static extern void CopyMemory(IntPtr destination, IntPtr source, uint length);

	public string GetAppDir()
	{
		return Application.StartupPath;
	}

	public string MakeAppDir(string sDirName)
	{
		string text = Application.StartupPath + Path.DirectorySeparatorChar + sDirName;
		CreateDir(text);
		return text;
	}

	public uint IsDirExist(string sPath)
	{
		if (Directory.Exists(sPath))
		{
			return 1u;
		}
		return 0u;
	}

	public uint IsFileExist(string sPath)
	{
		if (File.Exists(sPath))
		{
			return 1u;
		}
		return 0u;
	}

	public void CreateDir(string sPath)
	{
		Directory.CreateDirectory(sPath);
	}

	public static void DeleteDir(string sPath)
	{
		try
		{
			if (sPath[sPath.Length - 1] != Path.DirectorySeparatorChar)
			{
				sPath += Path.DirectorySeparatorChar;
			}
			string[] fileSystemEntries = Directory.GetFileSystemEntries(sPath);
			string[] array = fileSystemEntries;
			foreach (string path in array)
			{
				if (Directory.Exists(path))
				{
					DeleteDir(sPath + Path.GetFileName(path));
				}
				else
				{
					File.Delete(sPath + Path.GetFileName(path));
				}
			}
			Directory.Delete(sPath, recursive: true);
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.ToString());
		}
	}

	public void SysTick_Start()
	{
		QueryPerformanceCounter(out start);
	}

	public void SysTick_Stop()
	{
		QueryPerformanceCounter(out stop);
	}

	public double SysTick_Duration()
	{
		long num = stop - start;
		QueryPerformanceFrequency(out frequency);
		return (double)num / (double)frequency;
	}

	public void Sleep(int time)
	{
		try
		{
			Thread.Sleep(time);
		}
		catch (ThreadInterruptedException ex)
		{
			MessageBox.Show(ex.Message.ToString());
		}
	}

	public void SaveBinaryFile(string sFilePath, byte[] buf, uint dwLen)
	{
		using BinaryWriter binaryWriter = new BinaryWriter(File.Open(sFilePath, FileMode.Create));
		binaryWriter.Write(buf, 0, (int)dwLen);
		binaryWriter.Close();
	}

	public void LoadBinaryFile(string sFilePath, byte[] buf, uint dwLen)
	{
		using BinaryReader binaryReader = new BinaryReader(File.OpenRead(sFilePath));
		binaryReader.Read(buf, 0, (int)dwLen);
		binaryReader.Close();
	}

	public uint GetFileSize(string sFilePath)
	{
		FileInfo fileInfo = new FileInfo(sFilePath);
		return (uint)fileInfo.Length;
	}

	protected void writeWord(BinaryWriter stream, int value)
	{
		stream.Write(new byte[2]
		{
			(byte)(value & 0xFF),
			(byte)((value >> 8) & 0xFF)
		});
	}

	protected void writeDword(BinaryWriter stream, long value)
	{
		stream.Write(new byte[4]
		{
			(byte)(value & 0xFF),
			(byte)((value >> 8) & 0xFF),
			(byte)((value >> 16) & 0xFF),
			(byte)((value >> 24) & 0xFF)
		});
	}

	protected void writeLong(BinaryWriter stream, long value)
	{
		stream.Write(new byte[4]
		{
			(byte)(value & 0xFF),
			(byte)((value >> 8) & 0xFF),
			(byte)((value >> 16) & 0xFF),
			(byte)((value >> 24) & 0xFF)
		});
	}

	public static byte[] IntToByteArray(int num)
	{
		byte[] array = new byte[4];
		array[3] = (byte)((num >> 24) & 0xFF);
		array[2] = (byte)((num >> 16) & 0xFF);
		array[1] = (byte)((num >> 8) & 0xFF);
		array[0] = (byte)(num & 0xFF);
		return array;
	}

	public static byte[] ShortToByteArray(short num)
	{
		byte[] array = new byte[2];
		array[1] = (byte)((num >> 8) & 0xFF);
		array[0] = (byte)(num & 0xFF);
		return array;
	}

	public static int byteToInt(byte[] b)
	{
		int num = 0;
		int num2 = b[0] & 0xFF;
		int num3 = b[1] & 0xFF;
		int num4 = b[2] & 0xFF;
		int num5 = b[3] & 0xFF;
		num5 <<= 24;
		num4 <<= 16;
		num3 <<= 8;
		return num2 | num3 | num4 | num5;
	}

	public static short byteToShort(byte[] b)
	{
		short num = 0;
		short num2 = (short)(b[0] & 0xFF);
		short num3 = (short)(b[1] & 0xFF);
		num3 <<= 8;
		return (short)(num2 | num3);
	}

	private static void Swap<T>(ref T x, ref T y)
	{
		T val = y;
		y = x;
		x = val;
	}

	public uint Build()
	{
		IPicConvert[] array = new IPicConvert[m_ctlImage.Items.Count];
		Stream[] array2 = new Stream[m_ctlImage.Items.Count];
		MemoryStream memoryStream = new MemoryStream();
		IPicConfig picConfig = new UIPicConfig();
		if (frm2 == null)
		{
			picConfig.outputType = "565";
			picConfig.isHeaderAdd = true;
			picConfig.isMSBFirst = true;
			picConfig.compress = "RLE";
			picConfig.isResizeAll = false;
			picConfig.resizeHeight = 0;
			picConfig.resizeWidth = 0;
		}
		else
		{
			if (frm2.m_ctlBmpType1.Checked)
			{
				picConfig.outputType = "565";
			}
			if (frm2.m_ctlBmpType2.Checked)
			{
				picConfig.outputType = "888";
			}
			if (frm2.m_ctlHeader.Checked)
			{
				picConfig.isHeaderAdd = true;
			}
			else
			{
				picConfig.isHeaderAdd = false;
			}
			if (frm2.m_ctlEndian.Checked)
			{
				picConfig.isMSBFirst = true;
			}
			else
			{
				picConfig.isMSBFirst = false;
			}
			if (frm2.isCompress.Checked)
			{
				picConfig.compress = "RLE";
			}
			else
			{
				picConfig.compress = "NONE";
			}
			if (frm2.m_ctlResize.Checked)
			{
				picConfig.isResizeAll = true;
				picConfig.resizeHeight = int.Parse(frm2.m_ctlResizeHeight.Text);
				picConfig.resizeWidth = int.Parse(frm2.m_ctlResizeWidth.Text);
			}
			else
			{
				picConfig.isResizeAll = false;
				picConfig.resizeHeight = 0;
				picConfig.resizeWidth = 0;
			}
		}
		for (int i = 0; i < m_ctlImage.Items.Count; i++)
		{
			picConfig.filename = maps[m_ctlImage.Items[i].ToString()];
			PicConvertFactory picConvertFactory = new PicConvertFactory();
			CompressFactory compressFactory = new CompressFactory();
			int num = picConfig.filename.LastIndexOf('.');
			string type = picConfig.filename.Substring(num + 1);
			IPicConvert picConvert = picConvertFactory.createPicConvert(type);
			ICompress icp = compressFactory.createCompress(picConfig.compress);
			if (picConvert != null)
			{
				picConvert.setPicConfig(picConfig);
				array[i] = picConvert;
				array2[i] = picConvert.getConvertedData(icp);
			}
			Debug.WriteLine("Convert image : " + picConfig.filename);
			picConvertFactory = null;
			compressFactory = null;
			icp = null;
			int num2 = (i + 1) * 100 / array.Length;
			if (num2 > 100)
			{
				num2 = 100;
			}
			if (num2 < 0)
			{
				num2 = 0;
			}
			UpdateProgressBar("Image Converting", num2);
		}
		Debug.WriteLine("Image converted");
		Debug.WriteLine("Creating image bin");
		if (picConfig.isHeaderAdd)
		{
			ushort value = (ushort)array.Length;
			memoryStream.Write(BitConverter.GetBytes(value), 0, 2);
			uint num3 = 0u;
			for (int i = 0; i < array.Length; i++)
			{
				byte[] array3 = new byte[10];
				num3 = ((i != 0) ? (num3 + (uint)array[i].imageConvertedSize) : ((uint)(2 + array.Length * 10 + array[i].imageConvertedSize)));
				uint value2 = num3 - 1;
				byte[] bytes = BitConverter.GetBytes(value2);
				Array.Copy(bytes, array3, bytes.Length);
				bytes = BitConverter.GetBytes((ushort)array[i].imageWidth);
				Array.Copy(bytes, 0, array3, 4, bytes.Length);
				bytes = BitConverter.GetBytes((ushort)array[i].imageHeight);
				Array.Copy(bytes, 0, array3, 6, bytes.Length);
				bytes = BitConverter.GetBytes(array[i].imageFormat);
				Array.Copy(bytes, 0, array3, 8, bytes.Length);
				memoryStream.Write(array3, 0, array3.Length);
				array[i] = null;
				array3 = null;
				bytes = null;
			}
		}
		for (int i = 0; i < array.Length; i++)
		{
			array2[i].CopyTo(memoryStream);
			array2[i] = null;
		}
		memoryStream.Seek(0L, SeekOrigin.Begin);
		string sFilePath = Application.StartupPath + "\\Image3.bin";
		if (m_sFilePath == null || m_sFilePath == "")
		{
			m_sFilePath = sFilePath;
		}
		FileStream fileStream = File.Open(m_sFilePath, FileMode.Create);
		memoryStream.CopyTo(fileStream);
		fileStream.Flush();
		fileStream.Close();
		memoryStream.Seek(0L, SeekOrigin.Begin);
		m_pCodData = new byte[memoryStream.Length];
		memoryStream.Read(m_pCodData, 0, m_pCodData.Length);
		m_DataX_len = (uint)m_pCodData.Length;
		fileStream = null;
		memoryStream = null;
		array2 = null;
		array = null;
		UpdateProgressBar("Done", 0);
		Debug.WriteLine("Image bin created");
		return 0u;
	}

	public uint GetRawDataBuffer(string sFilePath, out uint dwTotalSize)
	{
		dwTotalSize = 0u;
		byte[] array = new byte[3];
		byte[] array2 = new byte[4];
		byte[] array3 = new byte[4];
		FileInfo fileInfo = new FileInfo(sFilePath);
		long length = fileInfo.Length;
		readBytes = new byte[length];
		BinaryReader binaryReader = new BinaryReader(File.OpenRead(sFilePath));
		binaryReader.Read(readBytes, 0, (int)length);
		binaryReader.Close();
		Array.Copy(readBytes, 0, array2, 0, 2);
		short num = byteToShort(array2);
		Array.Copy(readBytes, 2, array2, 0, 4);
		int num2 = byteToInt(array2);
		Array.Copy(readBytes, 10, array2, 0, 4);
		int num3 = byteToInt(array2);
		Array.Copy(readBytes, 18, array2, 0, 4);
		uint num4 = (uint)byteToInt(array2);
		Array.Copy(readBytes, 22, array2, 0, 4);
		uint num5 = (uint)byteToInt(array2);
		Array.Copy(readBytes, 34, array2, 0, 4);
		int num6 = byteToInt(array2);
		byte b = readBytes[28];
		if (num != 19778)
		{
			MessageBox.Show("不是點陣圖格式!");
			return 255u;
		}
		if (b != 24)
		{
			MessageBox.Show("不是24位元點陣圖!");
			return 255u;
		}
		if (num2 == 0)
		{
			MessageBox.Show("不是正確BMP文件的大小!");
			return 255u;
		}
		num2 = (int)(length - num3);
		byte[] array4 = new byte[length - num3];
		Array.Copy(readBytes, num3, array4, 0, num2);
		int num7 = 0;
		if (num4 % 4 != 0)
		{
			uint num8 = 0u;
			for (num7 = 0; num7 < num5; num7++)
			{
				for (uint num9 = 0u; num9 < num4; num9++)
				{
					array4[num8] = array4[num7 * (num4 * 3 + num4 % 4) + num9 * 3];
					array4[num8 + 1] = array4[num7 * (num4 * 3 + num4 % 4) + num9 * 3 + 1];
					array4[num8 + 2] = array4[num7 * (num4 * 3 + num4 % 4) + num9 * 3 + 2];
					num8 += 3;
				}
			}
		}
		if (m_iBMPType == 1)
		{
			uint num10 = (uint)(num2 * 2 / 3);
			rawBuffer = new byte[num10];
			PackageToBMP565(array4, rawBuffer, num4, num5, m_bIsEndian);
		}
		else if (m_iBMPType == 2)
		{
			uint num11 = (uint)num2;
			rawBuffer = new byte[num11];
			PackageToBMP888(array4, rawBuffer, num4, num5, m_bIsEndian);
		}
		return 0u;
	}

	private void PackageToBMP565(byte[] ucRawData, byte[] ucNewData, uint uWidth, uint uHeight, uint uEndian)
	{
		uint num = 0u;
		uint num2 = 0u;
		byte[] array = new byte[3];
		byte[] array2 = new byte[4];
		byte[] array3 = new byte[4];
		num = 0u;
		for (int i = 0; i < uWidth * uHeight; i++)
		{
			Array.Copy(ucRawData, num2, array2, 0L, 3L);
			Bitmap24ToRGB565(array2, array3);
			if (uEndian != 0)
			{
				Array.Copy(array3, 0L, ucNewData, num, 2L);
			}
			else
			{
				Swap(ref array3[0], ref array3[1]);
				Array.Copy(array3, 0L, ucNewData, num, 2L);
			}
			num2 += 3;
			num += 2;
		}
	}

	private void PackageToBMP888(byte[] ucRawData, byte[] ucNewData, uint uWidth, uint uHeight, uint uEndian)
	{
		uint num = 0u;
		uint num2 = 0u;
		byte[] array = new byte[3];
		byte[] array2 = new byte[4];
		byte[] array3 = new byte[4];
		num = 0u;
		for (int i = 0; i < uWidth * uHeight; i++)
		{
			Array.Copy(ucRawData, num2, array2, 0L, 3L);
			if (uEndian != 0)
			{
				array3[2] = array2[2];
				array3[1] = array2[1];
				array3[0] = array2[0];
				Array.Copy(array3, 0L, ucNewData, num, 3L);
			}
			else
			{
				array3[2] = array2[0];
				array3[1] = array2[1];
				array3[0] = array2[2];
				Array.Copy(array3, 0L, ucNewData, num, 3L);
			}
			num2 += 3;
			num += 3;
		}
	}

	public void Bitmap24ToRGB565(byte[] pRGB888, byte[] pRGB565)
	{
		int num = (pRGB888[0] >> 3) | ((pRGB888[1] & 0xFC) << 3) | ((pRGB888[2] & 0xF8) << 8);
		pRGB565[1] = (byte)(num >> 8);
		pRGB565[0] = (byte)(num & 0xFF);
	}

	public void SaveBitmap(Bitmap bm, string sPath)
	{
		int num = bm.Width;
		int num2 = bm.Height;
		int num3 = num2 * (num * 3 + num % 4) + 2;
		using (Stream stream = new FileStream(sPath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
		{
			using BinaryWriter binaryWriter = new BinaryWriter(stream);
			int value = 19778;
			long value2 = 54 + num3;
			int value3 = 0;
			int value4 = 0;
			long value5 = 54L;
			writeWord(binaryWriter, value);
			writeDword(binaryWriter, value2);
			writeWord(binaryWriter, value3);
			writeWord(binaryWriter, value4);
			writeDword(binaryWriter, value5);
			long value6 = 40L;
			long value7 = num;
			long value8 = num2;
			int value9 = 1;
			int value10 = 24;
			long value11 = 0L;
			long value12 = num * num2 * 3;
			long value13 = 0L;
			long value14 = 0L;
			long value15 = 0L;
			long value16 = 0L;
			writeDword(binaryWriter, value6);
			writeLong(binaryWriter, value7);
			writeLong(binaryWriter, value8);
			writeWord(binaryWriter, value9);
			writeWord(binaryWriter, value10);
			writeDword(binaryWriter, value11);
			writeDword(binaryWriter, value12);
			writeLong(binaryWriter, value13);
			writeLong(binaryWriter, value14);
			writeDword(binaryWriter, value15);
			writeDword(binaryWriter, value16);
			byte[] array = new byte[num3];
			int num4 = num * 3 + num % 4;
			int num5 = 0;
			int num6 = num2 - 1;
			while (num5 < num2)
			{
				int num7 = 0;
				int num8 = 0;
				while (num7 < num)
				{
					Color pixel = bm.GetPixel(num7, num5);
					array[num6 * num4 + num8] = pixel.B;
					array[num6 * num4 + num8 + 1] = pixel.G;
					array[num6 * num4 + num8 + 2] = pixel.R;
					num7++;
					num8 += 3;
				}
				num5++;
				num6--;
			}
			binaryWriter.Write(array);
			binaryWriter.Flush();
			binaryWriter.Close();
			stream.Close();
			stream.Dispose();
		}
		GC.Collect();
	}

	public static Bitmap ResizeImage(Bitmap imgToResize, int Width, int Height)
	{
		Bitmap bitmap = new Bitmap(Width, Height);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			graphics.DrawImage(imgToResize, 0, 0, Width, Height);
		}
		return bitmap;
	}

	public static Bitmap ResizeImage(Bitmap imgToResize, Size size)
	{
		Bitmap bitmap = new Bitmap(size.Width, size.Height);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			graphics.DrawImage(imgToResize, 0, 0, size.Width, size.Height);
		}
		return bitmap;
	}

	[DllImport("USB_DLL.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern uint HTUSB_Open(ushort wPID, ushort wVID);

	[DllImport("USB_DLL.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern uint HTUSB_Close();

	[DllImport("USB_DLL.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern uint HTUSB_SetTimeouts(uint ReadTimeout, uint WriteTimeout);

	[DllImport("USB_DLL.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern uint HTUSB_GetTimeouts(uint[] ReadTimeout, uint[] WriteTimeout);

	[DllImport("USB_DLL.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern uint HTUSB_WriteData(uint dwSize, byte[] pBuf);

	[DllImport("USB_DLL.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern uint HTUSB_ReadData(uint dwSize, byte[] pBuf);

	private void updateUSBStatus()
	{
		if (FindDevice() == 0)
		{
			m_labelDevInfo.Text = Lable.USB_connected;
			getVersion();
		}
		else
		{
			m_labelDevInfo.Text = Lable.USB_disconnected;
			updateVersionText("N/A");
		}
	}

	private void updateThread(object data)
	{
		Invoke(new MethodInvoker(updateUSBStatus));
	}

	protected override void WndProc(ref Message m)
	{
		int msg = m.Msg;
		if (msg == 537)
		{
			OnDeviceChange(ref m);
		}
		base.WndProc(ref m);
	}

	private void OnDeviceChange(ref Message msg)
	{
		int num = (int)msg.WParam;
		if (num == 32768 || num == 32772 || num == 7)
		{
			thread_usb = new Thread(updateThread);
			thread_usb.Start();
		}
	}

	private void RegisterHidNotification()
	{
		Win32.DEV_BROADCAST_DEVICEINTERFACE dEV_BROADCAST_DEVICEINTERFACE = new Win32.DEV_BROADCAST_DEVICEINTERFACE();
		int cb = (dEV_BROADCAST_DEVICEINTERFACE.dbcc_size = Marshal.SizeOf((object)dEV_BROADCAST_DEVICEINTERFACE));
		dEV_BROADCAST_DEVICEINTERFACE.dbcc_devicetype = 5;
		dEV_BROADCAST_DEVICEINTERFACE.dbcc_reserved = 0;
		dEV_BROADCAST_DEVICEINTERFACE.dbcc_classguid = Win32.GUID_DEVINTERFACE_HID;
		dEV_BROADCAST_DEVICEINTERFACE.dbcc_name = 0;
		IntPtr intPtr = Marshal.AllocHGlobal(cb);
		Marshal.StructureToPtr((object)dEV_BROADCAST_DEVICEINTERFACE, intPtr, true);
		IntPtr intPtr2 = Win32.RegisterDeviceNotification(Handle, intPtr, 0);
	}

	public uint IsDeviceConnect()
	{
		if (ConnectToUSBDevice(m_wVID, m_wPID) != 0)
		{
			return 255u;
		}
		DisConnectWriter();
		return 0u;
	}

	public uint ConnectToUSBDevice(ushort wPID, ushort wVID)
	{
		return HTUSB_Open(wPID, wVID);
	}

	public uint DisConnectWriter()
	{
		return HTUSB_Close();
	}

	private bool updateVersionText(string str)
	{
		if (str != null)
		{
			if (m_lVersion.InvokeRequired)
			{
				UpdateInfoTextEventHandler method = updateVersionText;
				return (bool)Invoke(method, str);
			}
			m_lVersion.Text = str;
			return true;
		}
		return false;
	}

	private bool getVersion()
	{
		LCDPlatform lCDPlatform = new LCDPlatform();
		string version = lCDPlatform.getVersion();
		lCDPlatform.closeDevice();
		lCDPlatform = null;
		return updateVersionText(version ?? "");
	}
}
