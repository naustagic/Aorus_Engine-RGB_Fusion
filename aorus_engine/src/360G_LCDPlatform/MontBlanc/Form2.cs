using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace MontBlanc;

public class Form2 : Form
{
	private Form1 mainForm;

	private IContainer components = null;

	private GroupBox m_groupBox1;

	private GroupBox m_groupBox2;

	public RadioButton m_ctlBmpType1;

	public RadioButton m_ctlBmpType2;

	private GroupBox m_groupBox3;

	public CheckBox m_ctlHeader;

	public CheckBox m_ctlEndian;

	public CheckBox m_ctlResize;

	public TextBox m_ctlResizeHeight;

	public TextBox m_ctlResizeWidth;

	public Label m_labelHeight;

	public Label m_labelWidth;

	private Button m_ctlOptionOK;

	private GroupBox groupBox1;

	public RadioButton compressRLE2;

	public RadioButton compressRLE;

	public RadioButton compressNONE;

	public CheckBox isCompress;

	public Form2(Form1 mainForm)
	{
		this.mainForm = mainForm;
		InitializeComponent();
		Init();
	}

	public void Init()
	{
		InitView();
	}

	public void InitView()
	{
		BackColor = Color.FromArgb(244, 245, 246);
		m_groupBox1.Font = new Font("Consolas", 12f, m_groupBox1.Font.Style);
		m_groupBox2.Font = new Font("Consolas", 12f, m_groupBox2.Font.Style);
		m_groupBox3.Font = new Font("Consolas", 12f, m_groupBox3.Font.Style);
		m_ctlHeader.Font = new Font("Consolas", 12f, m_ctlHeader.Font.Style);
		m_ctlEndian.Font = new Font("Consolas", 12f, m_ctlEndian.Font.Style);
		m_labelWidth.Font = new Font("Consolas", 12f, m_labelWidth.Font.Style);
		m_labelHeight.Font = new Font("Consolas", 12f, m_labelHeight.Font.Style);
		m_ctlOptionOK.Font = new Font("Consolas", 12f, m_ctlOptionOK.Font.Style);
		m_ctlOptionOK.FlatStyle = FlatStyle.Flat;
		m_ctlOptionOK.BackColor = Color.Green;
		m_ctlOptionOK.ForeColor = Color.White;
	}

	private void Form2_Load(object sender, EventArgs e)
	{
		UpdateComponent(0u);
	}

	private void m_ctlOptionOK_Click(object sender, EventArgs e)
	{
		UpdateComponent(1u);
		Close();
	}

	private void UpdateComponent(uint bSaveAndValidate)
	{
		if (bSaveAndValidate == 1)
		{
			if (m_ctlResize.Checked)
			{
				mainForm.m_bIsResize = 1u;
			}
			else
			{
				mainForm.m_bIsResize = 0u;
			}
			if (m_ctlHeader.Checked)
			{
				mainForm.m_bIsHeader = 1u;
			}
			else
			{
				mainForm.m_bIsHeader = 0u;
			}
			if (m_ctlEndian.Checked)
			{
				mainForm.m_bIsEndian = 1u;
			}
			else
			{
				mainForm.m_bIsEndian = 0u;
			}
			if (m_ctlBmpType1.Checked)
			{
				mainForm.m_iBMPType = 1;
			}
			else if (m_ctlBmpType2.Checked)
			{
				mainForm.m_iBMPType = 2;
			}
		}
		else
		{
			if (mainForm.m_bIsResize == 1)
			{
				m_ctlResize.Checked = true;
			}
			else
			{
				m_ctlResize.Checked = false;
			}
			if (mainForm.m_bIsHeader == 1)
			{
				m_ctlHeader.Checked = true;
			}
			else
			{
				m_ctlHeader.Checked = false;
			}
			if (mainForm.m_bIsEndian == 1)
			{
				m_ctlEndian.Checked = true;
			}
			else
			{
				m_ctlEndian.Checked = false;
			}
			if (mainForm.m_iBMPType == 1)
			{
				m_ctlBmpType1.Checked = true;
			}
			else if (mainForm.m_iBMPType == 2)
			{
				m_ctlBmpType2.Checked = true;
			}
		}
	}

	private void m_btnSaveBinPath_Click(object sender, EventArgs e)
	{
		SaveFileDialog saveFileDialog = new SaveFileDialog();
		saveFileDialog.Filter = "Bin files (*.bin)|*.bin|All files (*.*)|*.*";
		saveFileDialog.FilterIndex = 1;
		saveFileDialog.RestoreDirectory = true;
		if (saveFileDialog.ShowDialog() != DialogResult.OK)
		{
		}
	}

	private void m_btnLoadBinPath_Click(object sender, EventArgs e)
	{
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
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MontBlanc.Form2));
		this.m_groupBox1 = new System.Windows.Forms.GroupBox();
		this.m_ctlBmpType2 = new System.Windows.Forms.RadioButton();
		this.m_ctlBmpType1 = new System.Windows.Forms.RadioButton();
		this.m_groupBox2 = new System.Windows.Forms.GroupBox();
		this.m_ctlResizeHeight = new System.Windows.Forms.TextBox();
		this.m_ctlResizeWidth = new System.Windows.Forms.TextBox();
		this.m_labelHeight = new System.Windows.Forms.Label();
		this.m_labelWidth = new System.Windows.Forms.Label();
		this.m_ctlResize = new System.Windows.Forms.CheckBox();
		this.m_groupBox3 = new System.Windows.Forms.GroupBox();
		this.m_ctlEndian = new System.Windows.Forms.CheckBox();
		this.m_ctlHeader = new System.Windows.Forms.CheckBox();
		this.m_ctlOptionOK = new System.Windows.Forms.Button();
		this.groupBox1 = new System.Windows.Forms.GroupBox();
		this.isCompress = new System.Windows.Forms.CheckBox();
		this.compressRLE2 = new System.Windows.Forms.RadioButton();
		this.compressRLE = new System.Windows.Forms.RadioButton();
		this.compressNONE = new System.Windows.Forms.RadioButton();
		this.m_groupBox1.SuspendLayout();
		this.m_groupBox2.SuspendLayout();
		this.m_groupBox3.SuspendLayout();
		this.groupBox1.SuspendLayout();
		base.SuspendLayout();
		this.m_groupBox1.Controls.Add(this.m_ctlBmpType2);
		this.m_groupBox1.Controls.Add(this.m_ctlBmpType1);
		resources.ApplyResources(this.m_groupBox1, "m_groupBox1");
		this.m_groupBox1.Name = "m_groupBox1";
		this.m_groupBox1.TabStop = false;
		resources.ApplyResources(this.m_ctlBmpType2, "m_ctlBmpType2");
		this.m_ctlBmpType2.Name = "m_ctlBmpType2";
		this.m_ctlBmpType2.TabStop = true;
		this.m_ctlBmpType2.UseVisualStyleBackColor = true;
		resources.ApplyResources(this.m_ctlBmpType1, "m_ctlBmpType1");
		this.m_ctlBmpType1.Name = "m_ctlBmpType1";
		this.m_ctlBmpType1.TabStop = true;
		this.m_ctlBmpType1.UseVisualStyleBackColor = true;
		this.m_groupBox2.Controls.Add(this.m_ctlResizeHeight);
		this.m_groupBox2.Controls.Add(this.m_ctlResizeWidth);
		this.m_groupBox2.Controls.Add(this.m_labelHeight);
		this.m_groupBox2.Controls.Add(this.m_labelWidth);
		this.m_groupBox2.Controls.Add(this.m_ctlResize);
		resources.ApplyResources(this.m_groupBox2, "m_groupBox2");
		this.m_groupBox2.Name = "m_groupBox2";
		this.m_groupBox2.TabStop = false;
		resources.ApplyResources(this.m_ctlResizeHeight, "m_ctlResizeHeight");
		this.m_ctlResizeHeight.Name = "m_ctlResizeHeight";
		resources.ApplyResources(this.m_ctlResizeWidth, "m_ctlResizeWidth");
		this.m_ctlResizeWidth.Name = "m_ctlResizeWidth";
		resources.ApplyResources(this.m_labelHeight, "m_labelHeight");
		this.m_labelHeight.Name = "m_labelHeight";
		resources.ApplyResources(this.m_labelWidth, "m_labelWidth");
		this.m_labelWidth.Name = "m_labelWidth";
		resources.ApplyResources(this.m_ctlResize, "m_ctlResize");
		this.m_ctlResize.Name = "m_ctlResize";
		this.m_ctlResize.UseVisualStyleBackColor = true;
		this.m_groupBox3.Controls.Add(this.m_ctlEndian);
		this.m_groupBox3.Controls.Add(this.m_ctlHeader);
		resources.ApplyResources(this.m_groupBox3, "m_groupBox3");
		this.m_groupBox3.Name = "m_groupBox3";
		this.m_groupBox3.TabStop = false;
		resources.ApplyResources(this.m_ctlEndian, "m_ctlEndian");
		this.m_ctlEndian.Name = "m_ctlEndian";
		this.m_ctlEndian.UseVisualStyleBackColor = true;
		resources.ApplyResources(this.m_ctlHeader, "m_ctlHeader");
		this.m_ctlHeader.Name = "m_ctlHeader";
		this.m_ctlHeader.UseVisualStyleBackColor = true;
		resources.ApplyResources(this.m_ctlOptionOK, "m_ctlOptionOK");
		this.m_ctlOptionOK.Name = "m_ctlOptionOK";
		this.m_ctlOptionOK.UseVisualStyleBackColor = true;
		this.m_ctlOptionOK.Click += new System.EventHandler(this.m_ctlOptionOK_Click);
		this.groupBox1.Controls.Add(this.isCompress);
		this.groupBox1.Controls.Add(this.compressRLE2);
		this.groupBox1.Controls.Add(this.compressRLE);
		this.groupBox1.Controls.Add(this.compressNONE);
		resources.ApplyResources(this.groupBox1, "groupBox1");
		this.groupBox1.Name = "groupBox1";
		this.groupBox1.TabStop = false;
		resources.ApplyResources(this.isCompress, "isCompress");
		this.isCompress.Name = "isCompress";
		this.isCompress.UseVisualStyleBackColor = true;
		resources.ApplyResources(this.compressRLE2, "compressRLE2");
		this.compressRLE2.Name = "compressRLE2";
		this.compressRLE2.UseVisualStyleBackColor = true;
		resources.ApplyResources(this.compressRLE, "compressRLE");
		this.compressRLE.Checked = true;
		this.compressRLE.Name = "compressRLE";
		this.compressRLE.TabStop = true;
		this.compressRLE.UseVisualStyleBackColor = true;
		resources.ApplyResources(this.compressNONE, "compressNONE");
		this.compressNONE.Name = "compressNONE";
		this.compressNONE.UseVisualStyleBackColor = true;
		resources.ApplyResources(this, "$this");
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.Controls.Add(this.groupBox1);
		base.Controls.Add(this.m_ctlOptionOK);
		base.Controls.Add(this.m_groupBox3);
		base.Controls.Add(this.m_groupBox2);
		base.Controls.Add(this.m_groupBox1);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		base.MaximizeBox = false;
		base.Name = "Form2";
		base.TopMost = true;
		base.Load += new System.EventHandler(this.Form2_Load);
		this.m_groupBox1.ResumeLayout(false);
		this.m_groupBox1.PerformLayout();
		this.m_groupBox2.ResumeLayout(false);
		this.m_groupBox2.PerformLayout();
		this.m_groupBox3.ResumeLayout(false);
		this.m_groupBox3.PerformLayout();
		this.groupBox1.ResumeLayout(false);
		this.groupBox1.PerformLayout();
		base.ResumeLayout(false);
	}
}
