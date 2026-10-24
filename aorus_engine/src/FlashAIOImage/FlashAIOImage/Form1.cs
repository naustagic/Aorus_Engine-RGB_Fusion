using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using FlashAIOImage.Resources;
using FlashAIOImage.device;

namespace FlashAIOImage;

public class Form1 : Form, IProgress
{
	private string[] m_args;

	public byte[] m_pCodData;

	public Thread m_wt;

	public Thread m_retry;

	public string m_sFilePath;

	public uint m_dwFileSize;

	public uint m_DataX_len;

	public bool m_bFlashing;

	private Brush m_brush;

	private string progressLable = "";

	private IContainer components;

	private ProgressBar m_ctlProgressBar1;

	private Label m_labelDevInfo;

	public Form1(string[] args)
	{
		m_args = args;
		InitializeComponent();
		base.Shown += Form1_Shown;
	}

	private void Form1_Shown(object? sender, EventArgs e)
	{
		string? targetFile = null;

		if (m_args != null && m_args.Length > 0 && !string.IsNullOrWhiteSpace(m_args[0]))
		{
			targetFile = m_args[0];
		}
		else
		{
			using var ofd = new OpenFileDialog
			{
				Title = "Selecione o arquivo .BIN ou Imagem para gravar no LCD",
				Filter = "Arquivos Suportados (*.bin;*.gif;*.png;*.jpg;*.bmp;*.webp)|*.bin;*.gif;*.png;*.jpg;*.jpeg;*.bmp;*.webp|Arquivo Binário (*.bin)|*.bin|Imagens (*.gif;*.png;*.jpg;*.bmp)|*.gif;*.png;*.jpg;*.jpeg;*.bmp|Todos os arquivos (*.*)|*.*",
				CheckFileExists = true
			};

			if (ofd.ShowDialog(this) == DialogResult.OK)
			{
				targetFile = ofd.FileName;
			}
			else
			{
				Close();
				return;
			}
		}

		if (string.IsNullOrEmpty(targetFile) || !LoadImage(targetFile))
		{
			MessageBox.Show("Não foi possível carregar o arquivo selecionado ou formato inválido.", "Erro de Arquivo", MessageBoxButtons.OK, MessageBoxIcon.Error);
			Close();
			return;
		}

		Flash();
	}

	public void Flash()
	{
		m_labelDevInfo.Text = "Conectando ao Water Cooler...";
		if (Connect())
		{
			m_bFlashing = true;
			Thread.Sleep(1500);
			InitThread();
			m_wt.Start(this);
		}
		else
		{
			MessageBox.Show("Dispositivo do Water Cooler ('LCD_PLATFORM') não encontrado!\nVerifique se o cabo USB do cooler está conectado.", "Dispositivo Não Encontrado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			Close();
		}
	}

	public uint GetFileSize(string sFilePath)
	{
		FileInfo fileInfo = new FileInfo(sFilePath);
		return (uint)fileInfo.Length;
	}

	public bool LoadImage(string inputTarget)
	{
		if (File.Exists(inputTarget))
		{
			string ext = Path.GetExtension(inputTarget).ToLowerInvariant();
			if (ext == ".bin")
			{
				m_DataX_len = GetFileSize(inputTarget);
				m_pCodData = new byte[m_DataX_len];
				LoadBinaryFile(inputTarget, m_pCodData, m_DataX_len);
				return m_DataX_len != 0;
			}
			else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".webp")
			{
				m_pCodData = AorusCoolerConverter.Core.AorusImageProcessor.ProcessImageToBin(inputTarget);
				m_DataX_len = (uint)m_pCodData.Length;
				return m_DataX_len != 0;
			}
		}

		string sFilePath = Directory.GetCurrentDirectory() + "\\images\\" + inputTarget + ".bin";
		if (File.Exists(sFilePath))
		{
			m_DataX_len = GetFileSize(sFilePath);
			m_pCodData = new byte[m_DataX_len];
			LoadBinaryFile(sFilePath, m_pCodData, m_DataX_len);
			return m_DataX_len != 0;
		}

		return false;
	}

	public void LoadBinaryFile(string sFilePath, byte[] buf, uint dwLen)
	{
		using BinaryReader binaryReader = new BinaryReader(File.OpenRead(sFilePath));
		binaryReader.Read(buf, 0, (int)dwLen);
		binaryReader.Close();
	}

	public void DisConnect()
	{
		HT32F hT32F = new HT32F();
		if (hT32F.findDevice() && hT32F.isIAPMode())
		{
			hT32F.rebootDevice(0);
		}
		m_bFlashing = false;
	}

	public bool Connect()
	{
		HT32F hT32F = new HT32F();
		if (hT32F.findDevice())
		{
			if (!hT32F.isIAPMode())
			{
				hT32F.rebootDevice(1);
			}
			hT32F.closeDevice();
			hT32F = null;
			return true;
		}
		return false;
	}

	private void InitThread()
	{
		try
		{
			m_wt = new Thread(FlashThread);
			m_retry = new Thread(RetryThread);
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message.ToString());
		}
	}

	public void RetryThread(object data)
	{
		Flash();
	}

	public void FlashThread(object data)
	{
		_ = (Form1)data;
		LCDPlatform lCDPlatform = new LCDPlatform();
		if (!lCDPlatform.findDevice())
		{
			Close();
			return;
		}
		if (m_pCodData.Length % 2048 != 0)
		{
			byte[] array = new byte[(m_pCodData.Length / 2048 + 1) * 2048];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = byte.MaxValue;
			}
			Array.Copy(m_pCodData, array, m_pCodData.Length);
			lCDPlatform.imageBinData = array;
		}
		else
		{
			lCDPlatform.imageBinData = m_pCodData;
		}
		bool flag = lCDPlatform.program(this);
		_ = lCDPlatform.totalTime;
		double apSpeed = lCDPlatform.apSpeed;
		double imageSpeed = lCDPlatform.imageSpeed;
		if (flag)
		{
			string text = "";
			if (lCDPlatform.APBinData != null && lCDPlatform.APBinData.Length > 0)
			{
				_ = "AP:" + ((double)lCDPlatform.APBinData.Length / 1024.0).ToString("#0.00") + " KB," + apSpeed.ToString("#0.00") + " KB/s.";
			}
			if (lCDPlatform.imageBinData != null && lCDPlatform.imageBinData.Length > 0)
			{
				text = ((lCDPlatform.imageBinData.Length >= 1048576) ? ("Image:" + ((double)lCDPlatform.imageBinData.Length / 1024.0 / 1024.0).ToString("#0.00") + " MB,") : ("Image:" + ((double)lCDPlatform.imageBinData.Length / 1024.0).ToString("#0.00") + " KB,"));
				text = text + imageSpeed.ToString("#0.00") + " KB/s.";
			}
			UpdateProgressBar(Lable.Done, 100);
		}
		else
		{
			UpdateProgressBar(Lable.Download_Fault, 100);
		}
		lCDPlatform.closeDevice();
		lCDPlatform = null;
		DisConnect();
		object[] args = new object[2]
		{
			this,
			EventArgs.Empty
		};
		Invoke(new EventHandler(CloseInvoke), args);
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

	private void CloseInvoke(object o, EventArgs e)
	{
		Close();
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

	private void Form1_FormClosed(object sender, FormClosedEventArgs e)
	{
		try
		{
			string aorusPath = Path.GetFullPath("../Aorus.exe");
			if (File.Exists(aorusPath))
			{
				Process.Start(new ProcessStartInfo(aorusPath, "/aio") { UseShellExecute = true });
			}
		}
		catch { }
	}

	private void Form1_FormClosing(object sender, FormClosingEventArgs e)
	{
		if (m_bFlashing)
		{
			e.Cancel = true;
			return;
		}
		try
		{
			DisConnect();
		}
		catch (Exception)
		{
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
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FlashAIOImage.Form1));
		this.m_ctlProgressBar1 = new System.Windows.Forms.ProgressBar();
		this.m_labelDevInfo = new System.Windows.Forms.Label();
		base.SuspendLayout();
		this.m_ctlProgressBar1.ImeMode = System.Windows.Forms.ImeMode.NoControl;
		this.m_ctlProgressBar1.Location = new System.Drawing.Point(11, 41);
		this.m_ctlProgressBar1.Name = "m_ctlProgressBar1";
		this.m_ctlProgressBar1.Size = new System.Drawing.Size(426, 18);
		this.m_ctlProgressBar1.TabIndex = 29;
		this.m_labelDevInfo.Font = new System.Drawing.Font("Courier New", 12f, System.Drawing.FontStyle.Bold);
		this.m_labelDevInfo.ImeMode = System.Windows.Forms.ImeMode.NoControl;
		this.m_labelDevInfo.Location = new System.Drawing.Point(20, 8);
		this.m_labelDevInfo.Name = "m_labelDevInfo";
		this.m_labelDevInfo.Size = new System.Drawing.Size(408, 29);
		this.m_labelDevInfo.TabIndex = 30;
		this.m_labelDevInfo.Text = "N/A";
		this.m_labelDevInfo.TextAlign = System.Drawing.ContentAlignment.TopCenter;
		// Credits
		var lblCredits = new System.Windows.Forms.Label
		{
			Text = "by SaraLapia  •  github.com/naustagic/Aorus_Engine-RGB_Fusion",
			Font = new System.Drawing.Font("Segoe UI", 7.5f, System.Drawing.FontStyle.Regular),
			ForeColor = System.Drawing.Color.FromArgb(100, 100, 110),
			BackColor = System.Drawing.Color.FromArgb(18, 18, 20),
			Location = new System.Drawing.Point(0, 65),
			Size = new System.Drawing.Size(449, 20),
			TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
			Cursor = System.Windows.Forms.Cursors.Hand,
			TabIndex = 31
		};
		lblCredits.Click += (_, _) =>
		{
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
			{
				FileName = "https://github.com/naustagic/Aorus_Engine-RGB_Fusion",
				UseShellExecute = true
			});
		};
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 12f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(449, 86);
		base.Controls.Add(lblCredits);
		base.Controls.Add(this.m_labelDevInfo);
		base.Controls.Add(this.m_ctlProgressBar1);
		base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
		base.Name = "Form1";
		this.Text = $"AIO Flasher v{AorusCoolerConverter.Core.AorusImageProcessor.AppVersion} — by SaraLapia";
		base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
		base.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
		base.ResumeLayout(false);
	}
}
