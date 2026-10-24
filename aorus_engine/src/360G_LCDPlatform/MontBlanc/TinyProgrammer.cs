#define DEBUG
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using MontBlanc.Resources;
using MontBlanc.device;
using MontBlanc.platform;

namespace MontBlanc;

public class TinyProgrammer : Form, IProgress
{
	private delegate void UpdateProgramButtonEventHandler(bool status);

	private delegate bool updateProgressBarEventHandler(int percent, int stage);

	private bool isUpdating = false;

	private Mutex mutex = null;

	private bool isAutoProgram = false;

	private bool isAutoProgramMsg = false;

	private bool isFinishClose = false;

	private string progressLable;

	private IContainer components = null;

	private Label label1;

	private Label lCurrentVersion;

	private Label label3;

	private Label lCurrentVersion2;

	private Label lNewVersion2;

	private Label lDeviceInfo2;

	private Label lPrograssInfo;

	private Button bProgram;

	private CheckBox enterAP;

	private ProgressBar progressBar;

	private Button bConnect;

	public TinyProgrammer()
	{
		InitializeComponent();
	}

	private void updateUSBStatus()
	{
		LCDPlatform lCDPlatform = new LCDPlatform();
		if (lCDPlatform != null && lCDPlatform.findDevice())
		{
			if (!lCDPlatform.isIAPMode())
			{
				if (!isAutoProgramMsg)
				{
					bProgram.Enabled = false;
					bConnect.Enabled = true;
				}
				else
				{
					bProgram.Enabled = false;
					bConnect.Enabled = false;
					HT32F hT32F = new HT32F();
					hT32F.rebootDevice(1);
					hT32F.closeDevice();
					hT32F = null;
				}
			}
			else
			{
				if (!isAutoProgramMsg)
				{
					bProgram.Enabled = true;
					bConnect.Enabled = false;
				}
				else
				{
					bProgram.Enabled = false;
					bConnect.Enabled = false;
				}
				lDeviceInfo2.Text = Lable.USB_connected;
				string version = lCDPlatform.getVersion();
				if (version != null)
				{
					lCurrentVersion2.Text = version;
				}
				if (isAutoProgram)
				{
					bProgram.Enabled = false;
					try
					{
						Thread thread = new Thread(programThread);
						thread.Name = "programThread";
						isUpdating = true;
						thread.Start(this);
					}
					catch (Exception ex)
					{
						MessageBox.Show(ex.Message.ToString());
					}
				}
			}
		}
		else
		{
			bProgram.Enabled = false;
			bConnect.Enabled = false;
			lDeviceInfo2.Text = Lable.USB_disconnected;
			lCurrentVersion2.Text = "N/A";
		}
		lCDPlatform.closeDevice();
		lCDPlatform = null;
	}

	private void updateThread(object data)
	{
		Invoke(new System.Windows.Forms.MethodInvoker(updateUSBStatus));
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
			new Thread(updateThread).Start();
		}
	}

	private void updateProgramButton(bool status)
	{
		if (bProgram.InvokeRequired)
		{
			UpdateProgramButtonEventHandler method = updateProgramButton;
			Invoke(method, status);
		}
		else
		{
			bProgram.Enabled = status;
		}
	}

	public void programThread(object data)
	{
		bool flag = false;
		isAutoProgram = false;
		if (isAutoProgramMsg)
		{
			updateProgress(0, 1);
			Thread.Sleep(2000);
		}
		string executablePath = Application.ExecutablePath;
		LCDPlatform lCDPlatform = new LCDPlatform();
		Reader instance = Reader.getInstance(executablePath);
		lCDPlatform.versionBinData = instance.getBinData(2);
		lCDPlatform.APBinData = instance.getBinData(1);
		lCDPlatform.imageBinData = instance.getBinData(0);
		flag = lCDPlatform.program(this);
		isAutoProgramMsg = false;
		if (enterAP.Checked)
		{
			lCDPlatform.rebootDevice(0);
		}
		updateProgramButton(status: true);
		isUpdating = false;
		lCDPlatform.closeDevice();
		lCDPlatform = null;
		if (!flag)
		{
			updateProgress(100, 11);
			return;
		}
		updateProgress(100, 10);
		if (isFinishClose)
		{
			Environment.Exit(Environment.ExitCode);
		}
	}

	private void bProgram_Click(object sender, EventArgs e)
	{
	}

	private void TinyProgramer_Load(object sender, EventArgs e)
	{
		Attribute customAttribute = Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(GuidAttribute));
		string value = ((GuidAttribute)customAttribute).Value;
		mutex = new Mutex(initiallyOwned: true, value, out var createdNew);
		if (!createdNew)
		{
			MessageBox.Show("'" + Assembly.GetExecutingAssembly().GetName().Name + ".exe' already is exists");
			mutex = null;
			Close();
		}
		string executablePath = Application.ExecutablePath;
		Reader instance = Reader.getInstance(executablePath);
		char[] array = new char[5];
		for (int i = 0; i < array.Length; i++)
		{
			byte[] binData = instance.getBinData(2);
			if (instance != null && binData != null && binData.Length > 0)
			{
				array[i] = (char)binData[i + 5];
			}
		}
		lNewVersion2.Text = new string(array);
		byte[] binData2 = instance.getBinData(3);
		if ((binData2[0] & 1) == 1)
		{
			if (binData2[0] == 3)
			{
				isFinishClose = true;
			}
			isAutoProgram = true;
			isAutoProgramMsg = true;
		}
		updateUSBStatus();
	}

	public bool updateProgress(int percent, int stage)
	{
		if (percent > 100)
		{
			percent = 100;
		}
		if (percent < 0)
		{
			percent = 0;
		}
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
		case 10:
			progressLable = Lable.Done;
			break;
		case 11:
			progressLable = Lable.Download_Fault;
			break;
		}
		if (progressBar.InvokeRequired)
		{
			updateProgressBarEventHandler method = updateProgress;
			return (bool)Invoke(method, percent, stage);
		}
		lPrograssInfo.Text = progressLable;
		progressBar.Value = percent;
		lPrograssInfo.Refresh();
		return true;
	}

	public bool setLable(string info)
	{
		progressLable = info;
		return true;
	}

	private void lPrograssInfo_Click(object sender, EventArgs e)
	{
	}

	private void TinyProgramer_FormClosing(object sender, FormClosingEventArgs e)
	{
		if (isUpdating)
		{
			e.Cancel = true;
		}
		if (mutex != null)
		{
			mutex.ReleaseMutex();
			mutex = null;
		}
		HT32F hT32F = new HT32F();
		if (hT32F.findDevice() && hT32F.isIAPMode())
		{
			hT32F.rebootDevice(0);
		}
		Environment.Exit(Environment.ExitCode);
	}

	private void bProgram_Click_1(object sender, EventArgs e)
	{
		bProgram.Enabled = false;
		try
		{
			Thread thread = new Thread(programThread);
			thread.Name = "programThread";
			isUpdating = true;
			thread.Start(this);
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message.ToString());
		}
	}

	private void progressBar_Click(object sender, EventArgs e)
	{
	}

	private void bProgram_Click_2(object sender, EventArgs e)
	{
		bProgram_Click_1(sender, e);
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
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MontBlanc.TinyProgrammer));
		this.label1 = new System.Windows.Forms.Label();
		this.lCurrentVersion = new System.Windows.Forms.Label();
		this.label3 = new System.Windows.Forms.Label();
		this.lCurrentVersion2 = new System.Windows.Forms.Label();
		this.lNewVersion2 = new System.Windows.Forms.Label();
		this.lDeviceInfo2 = new System.Windows.Forms.Label();
		this.lPrograssInfo = new System.Windows.Forms.Label();
		this.bProgram = new System.Windows.Forms.Button();
		this.enterAP = new System.Windows.Forms.CheckBox();
		this.progressBar = new System.Windows.Forms.ProgressBar();
		this.bConnect = new System.Windows.Forms.Button();
		base.SuspendLayout();
		resources.ApplyResources(this.label1, "label1");
		this.label1.Name = "label1";
		resources.ApplyResources(this.lCurrentVersion, "lCurrentVersion");
		this.lCurrentVersion.Name = "lCurrentVersion";
		resources.ApplyResources(this.label3, "label3");
		this.label3.Name = "label3";
		resources.ApplyResources(this.lCurrentVersion2, "lCurrentVersion2");
		this.lCurrentVersion2.Name = "lCurrentVersion2";
		resources.ApplyResources(this.lNewVersion2, "lNewVersion2");
		this.lNewVersion2.Name = "lNewVersion2";
		resources.ApplyResources(this.lDeviceInfo2, "lDeviceInfo2");
		this.lDeviceInfo2.Name = "lDeviceInfo2";
		resources.ApplyResources(this.lPrograssInfo, "lPrograssInfo");
		this.lPrograssInfo.Name = "lPrograssInfo";
		this.lPrograssInfo.Click += new System.EventHandler(this.lPrograssInfo_Click);
		resources.ApplyResources(this.bProgram, "bProgram");
		this.bProgram.Name = "bProgram";
		this.bProgram.UseVisualStyleBackColor = true;
		this.bProgram.Click += new System.EventHandler(this.bProgram_Click_2);
		resources.ApplyResources(this.enterAP, "enterAP");
		this.enterAP.Checked = true;
		this.enterAP.CheckState = System.Windows.Forms.CheckState.Checked;
		this.enterAP.Name = "enterAP";
		this.enterAP.UseVisualStyleBackColor = true;
		resources.ApplyResources(this.progressBar, "progressBar");
		this.progressBar.Name = "progressBar";
		this.progressBar.Click += new System.EventHandler(this.progressBar_Click);
		resources.ApplyResources(this.bConnect, "bConnect");
		this.bConnect.Name = "bConnect";
		this.bConnect.UseVisualStyleBackColor = true;
		this.bConnect.Click += new System.EventHandler(this.bConnect_Click);
		resources.ApplyResources(this, "$this");
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.Controls.Add(this.bConnect);
		base.Controls.Add(this.progressBar);
		base.Controls.Add(this.enterAP);
		base.Controls.Add(this.bProgram);
		base.Controls.Add(this.lPrograssInfo);
		base.Controls.Add(this.lDeviceInfo2);
		base.Controls.Add(this.lNewVersion2);
		base.Controls.Add(this.lCurrentVersion2);
		base.Controls.Add(this.label3);
		base.Controls.Add(this.lCurrentVersion);
		base.Controls.Add(this.label1);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		base.MaximizeBox = false;
		base.Name = "TinyProgrammer";
		base.TopMost = true;
		base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.TinyProgramer_FormClosing);
		base.Load += new System.EventHandler(this.TinyProgramer_Load);
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
