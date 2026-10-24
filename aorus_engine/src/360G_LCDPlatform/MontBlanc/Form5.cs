using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using MontBlanc.Resources;

namespace MontBlanc;

public class Form5 : Form
{
	private Form1 mainForm;

	private IContainer components = null;

	private Button m_ctlYes;

	private Button m_ctlNo;

	private GroupBox m_groupBox1;

	private RadioButton m_ctlDownloadType2;

	private RadioButton m_ctlDownloadType1;

	private CheckBox m_ckbtnEnterApMode;

	private CheckBox m_ckbtnDownload;

	private CheckBox m_cbDownloadAP;

	private TextBox m_tbVersion;

	private CheckBox m_ckbtnAutoCloseWhenFinsh;

	private CheckBox m_ckbtnAutoConnectProgram;

	private Label lVersion;

	public Form5(Form1 mainForm)
	{
		this.mainForm = mainForm;
		InitializeComponent();
		Init();
		m_ckbtnDownload.Checked = true;
		m_ctlDownloadType1.Checked = true;
		if (mainForm.m_bIsOneKeyProgram == 1)
		{
			m_ckbtnAutoConnectProgram.Checked = true;
			m_ckbtnAutoCloseWhenFinsh.Checked = false;
			return;
		}
		m_ckbtnAutoConnectProgram.Hide();
		m_ckbtnAutoCloseWhenFinsh.Hide();
		m_ckbtnAutoConnectProgram.Enabled = false;
		m_ckbtnAutoCloseWhenFinsh.Enabled = false;
	}

	public void Init()
	{
		InitView();
	}

	public void InitView()
	{
		BackColor = Color.FromArgb(244, 245, 246);
		m_ctlYes.Font = new Font("Consolas", 12f, m_ctlYes.Font.Style);
		m_ctlYes.FlatStyle = FlatStyle.Flat;
		m_ctlYes.BackColor = Color.Green;
		m_ctlYes.ForeColor = Color.White;
		m_ctlNo.Font = new Font("Consolas", 12f, m_ctlNo.Font.Style);
		m_ctlNo.FlatStyle = FlatStyle.Flat;
		m_groupBox1.Font = new Font("Consolas", 12f, m_groupBox1.Font.Style);
		m_ckbtnDownload.Font = new Font("Consolas", 12f, m_ckbtnDownload.Font.Style);
		m_ckbtnEnterApMode.Font = new Font("Consolas", 12f, m_ckbtnEnterApMode.Font.Style);
		m_ctlDownloadType1.Font = new Font("Consolas", 12f, m_ctlDownloadType1.Font.Style);
		m_ctlDownloadType2.Font = new Font("Consolas", 12f, m_ctlDownloadType2.Font.Style);
	}

	private void UpdateComponent(uint bSaveAndValidate)
	{
		if (bSaveAndValidate == 1)
		{
			if (m_ckbtnEnterApMode.Checked)
			{
				mainForm.m_bIsEntryAP = 1u;
			}
			else
			{
				mainForm.m_bIsEntryAP = 0u;
			}
			if (m_ckbtnDownload.Checked)
			{
				mainForm.m_bIsDownload = 1u;
			}
			else
			{
				mainForm.m_bIsDownload = 0u;
			}
			if (m_ctlDownloadType1.Checked)
			{
				mainForm.m_iFileType = 0;
			}
			else if (m_ctlDownloadType2.Checked)
			{
				mainForm.m_iFileType = 1;
			}
			if (m_cbDownloadAP.Checked)
			{
				mainForm.m_bIsDownloadAP = 1u;
			}
			else
			{
				mainForm.m_bIsDownloadAP = 0u;
			}
			if (m_ckbtnAutoConnectProgram.Checked)
			{
				mainForm.m_bIsAutoConnectProgram = 1u;
			}
			else
			{
				mainForm.m_bIsAutoConnectProgram = 0u;
			}
			if (m_ckbtnAutoCloseWhenFinsh.Checked)
			{
				mainForm.m_bIsProgramFinishCloseUI = 1u;
			}
			else
			{
				mainForm.m_bIsProgramFinishCloseUI = 0u;
			}
			mainForm.m_tNewVersion = m_tbVersion.Text;
		}
		else
		{
			if (mainForm.m_bIsEntryAP == 1)
			{
				m_ckbtnEnterApMode.Checked = true;
			}
			else
			{
				m_ckbtnEnterApMode.Checked = false;
			}
			if (mainForm.m_bIsDownload == 1)
			{
				m_ckbtnDownload.Checked = true;
			}
			else
			{
				m_ckbtnDownload.Checked = false;
			}
			if (mainForm.m_iFileType == 0)
			{
				m_ctlDownloadType1.Checked = true;
			}
			else if (mainForm.m_iFileType == 1)
			{
				m_ctlDownloadType2.Checked = true;
			}
			if (mainForm.m_bIsDownloadAP == 1)
			{
				m_cbDownloadAP.Checked = true;
			}
			else
			{
				m_cbDownloadAP.Checked = false;
			}
			if (mainForm.m_tNewVersion != null && !mainForm.m_tNewVersion.Equals(""))
			{
				m_tbVersion.Text = mainForm.m_tNewVersion;
			}
		}
	}

	private void m_ctlOK_Click(object sender, EventArgs e)
	{
		DialogResult = DialogResult.OK;
		UpdateComponent(1u);
		Close();
	}

	private void m_ctlCancel_Click(object sender, EventArgs e)
	{
		DialogResult = DialogResult.Cancel;
		Close();
	}

	private void Form5_Load(object sender, EventArgs e)
	{
		Location = new Point(mainForm.Location.X + 100, mainForm.Location.Y + 100);
		UpdateComponent(0u);
		ToolTip toolTip = new ToolTip();
		toolTip.AutoPopDelay = 10000;
		toolTip.InitialDelay = 1000;
		toolTip.ReshowDelay = 500;
		toolTip.IsBalloon = true;
		toolTip.ShowAlways = true;
		toolTip.SetToolTip(m_ckbtnDownload, Lable.setting_download_image);
		toolTip.SetToolTip(m_ctlDownloadType1, Lable.setting_images_in_the_list);
		toolTip.SetToolTip(m_ctlDownloadType2, Lable.setting_images_of_bin);
		toolTip.SetToolTip(m_cbDownloadAP, Lable.setting_download_ap);
		toolTip.SetToolTip(m_ckbtnEnterApMode, Lable.setting_entry_ap);
		toolTip.SetToolTip(lVersion, Lable.setting_set_version);
		toolTip.SetToolTip(m_ctlYes, Lable.setting_ok);
		toolTip.SetToolTip(m_ctlNo, Lable.setting_cancel);
	}

	private void radioButton2_CheckedChanged(object sender, EventArgs e)
	{
	}

	private void kbtnAutoConnectProgram_CheckedChanged(object sender, EventArgs e)
	{
		if (m_ckbtnAutoConnectProgram.Checked)
		{
			m_ckbtnAutoCloseWhenFinsh.Show();
		}
		else
		{
			m_ckbtnAutoCloseWhenFinsh.Hide();
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
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MontBlanc.Form5));
		this.m_ctlYes = new System.Windows.Forms.Button();
		this.m_ctlNo = new System.Windows.Forms.Button();
		this.m_groupBox1 = new System.Windows.Forms.GroupBox();
		this.m_ckbtnAutoCloseWhenFinsh = new System.Windows.Forms.CheckBox();
		this.m_ckbtnAutoConnectProgram = new System.Windows.Forms.CheckBox();
		this.lVersion = new System.Windows.Forms.Label();
		this.m_tbVersion = new System.Windows.Forms.TextBox();
		this.m_cbDownloadAP = new System.Windows.Forms.CheckBox();
		this.m_ckbtnEnterApMode = new System.Windows.Forms.CheckBox();
		this.m_ctlDownloadType2 = new System.Windows.Forms.RadioButton();
		this.m_ckbtnDownload = new System.Windows.Forms.CheckBox();
		this.m_ctlDownloadType1 = new System.Windows.Forms.RadioButton();
		this.m_groupBox1.SuspendLayout();
		base.SuspendLayout();
		resources.ApplyResources(this.m_ctlYes, "m_ctlYes");
		this.m_ctlYes.Name = "m_ctlYes";
		this.m_ctlYes.UseVisualStyleBackColor = true;
		this.m_ctlYes.Click += new System.EventHandler(this.m_ctlOK_Click);
		resources.ApplyResources(this.m_ctlNo, "m_ctlNo");
		this.m_ctlNo.ForeColor = System.Drawing.Color.Black;
		this.m_ctlNo.Name = "m_ctlNo";
		this.m_ctlNo.UseVisualStyleBackColor = true;
		this.m_ctlNo.Click += new System.EventHandler(this.m_ctlCancel_Click);
		this.m_groupBox1.Controls.Add(this.m_ckbtnAutoCloseWhenFinsh);
		this.m_groupBox1.Controls.Add(this.m_ckbtnAutoConnectProgram);
		this.m_groupBox1.Controls.Add(this.lVersion);
		this.m_groupBox1.Controls.Add(this.m_tbVersion);
		this.m_groupBox1.Controls.Add(this.m_cbDownloadAP);
		this.m_groupBox1.Controls.Add(this.m_ckbtnEnterApMode);
		this.m_groupBox1.Controls.Add(this.m_ctlDownloadType2);
		this.m_groupBox1.Controls.Add(this.m_ckbtnDownload);
		this.m_groupBox1.Controls.Add(this.m_ctlDownloadType1);
		resources.ApplyResources(this.m_groupBox1, "m_groupBox1");
		this.m_groupBox1.Name = "m_groupBox1";
		this.m_groupBox1.TabStop = false;
		resources.ApplyResources(this.m_ckbtnAutoCloseWhenFinsh, "m_ckbtnAutoCloseWhenFinsh");
		this.m_ckbtnAutoCloseWhenFinsh.Name = "m_ckbtnAutoCloseWhenFinsh";
		this.m_ckbtnAutoCloseWhenFinsh.UseVisualStyleBackColor = true;
		resources.ApplyResources(this.m_ckbtnAutoConnectProgram, "m_ckbtnAutoConnectProgram");
		this.m_ckbtnAutoConnectProgram.Name = "m_ckbtnAutoConnectProgram";
		this.m_ckbtnAutoConnectProgram.UseVisualStyleBackColor = true;
		this.m_ckbtnAutoConnectProgram.CheckedChanged += new System.EventHandler(this.kbtnAutoConnectProgram_CheckedChanged);
		resources.ApplyResources(this.lVersion, "lVersion");
		this.lVersion.Name = "lVersion";
		resources.ApplyResources(this.m_tbVersion, "m_tbVersion");
		this.m_tbVersion.Name = "m_tbVersion";
		resources.ApplyResources(this.m_cbDownloadAP, "m_cbDownloadAP");
		this.m_cbDownloadAP.Checked = true;
		this.m_cbDownloadAP.CheckState = System.Windows.Forms.CheckState.Checked;
		this.m_cbDownloadAP.Name = "m_cbDownloadAP";
		this.m_cbDownloadAP.UseVisualStyleBackColor = true;
		resources.ApplyResources(this.m_ckbtnEnterApMode, "m_ckbtnEnterApMode");
		this.m_ckbtnEnterApMode.Name = "m_ckbtnEnterApMode";
		this.m_ckbtnEnterApMode.UseVisualStyleBackColor = true;
		resources.ApplyResources(this.m_ctlDownloadType2, "m_ctlDownloadType2");
		this.m_ctlDownloadType2.Name = "m_ctlDownloadType2";
		this.m_ctlDownloadType2.TabStop = true;
		this.m_ctlDownloadType2.UseVisualStyleBackColor = true;
		resources.ApplyResources(this.m_ckbtnDownload, "m_ckbtnDownload");
		this.m_ckbtnDownload.Name = "m_ckbtnDownload";
		this.m_ckbtnDownload.UseVisualStyleBackColor = true;
		resources.ApplyResources(this.m_ctlDownloadType1, "m_ctlDownloadType1");
		this.m_ctlDownloadType1.Checked = true;
		this.m_ctlDownloadType1.Name = "m_ctlDownloadType1";
		this.m_ctlDownloadType1.TabStop = true;
		this.m_ctlDownloadType1.UseVisualStyleBackColor = true;
		resources.ApplyResources(this, "$this");
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.Controls.Add(this.m_groupBox1);
		base.Controls.Add(this.m_ctlNo);
		base.Controls.Add(this.m_ctlYes);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		base.MaximizeBox = false;
		base.Name = "Form5";
		base.TopMost = true;
		base.Load += new System.EventHandler(this.Form5_Load);
		this.m_groupBox1.ResumeLayout(false);
		this.m_groupBox1.PerformLayout();
		base.ResumeLayout(false);
	}
}
