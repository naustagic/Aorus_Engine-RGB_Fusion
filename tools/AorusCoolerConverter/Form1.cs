using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AorusCoolerConverter.Core;

namespace AorusCoolerConverter;

public partial class Form1 : Form
{
    private PictureBox picPreview;
    private Label lblTitle;
    private Label lblFileInfo;
    private ComboBox cmbRotation;
    private Button btnLoad;
    private Button btnSaveBin;
    private Button btnSaveBmp;
    private Button btnDetectCooler;
    private Label lblDeviceStatus;
    private TextBox txtGitHubRepo;
    private Button btnCheckUpdate;
    private Label lblUpdateStatus;
    private System.Windows.Forms.Timer animationTimer;

    private List<Bitmap> loadedFrames = new();
    private int currentFrameIndex = 0;
    private string? currentLoadedPath;

    public Form1()
    {
        InitializeComponent();
        SetupCustomUi();
    }

    private void SetupCustomUi()
    {
        this.Text = "AORUS Liquid Cooler — LCD Image & Animation Tool v3.0";
        this.Size = new Size(820, 640);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(24, 24, 27);
        this.ForeColor = Color.FromArgb(244, 244, 245);
        this.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;

        // Title
        lblTitle = new Label
        {
            Text = "AORUS Liquid Cooler LCD Studio",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(250, 146, 60), // Aorus Orange
            Location = new Point(24, 20),
            AutoSize = true
        };
        this.Controls.Add(lblTitle);

        // Subtitle
        var lblSub = new Label
        {
            Text = "Suporta PNG, JPG, BMP e GIF Animado nativamente (sem restrição de 16 bits)",
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(161, 161, 170),
            Location = new Point(26, 54),
            AutoSize = true
        };
        this.Controls.Add(lblSub);

        // Circular Preview PictureBox
        picPreview = new PictureBox
        {
            Location = new Point(24, 90),
            Size = new Size(340, 340),
            BackColor = Color.FromArgb(15, 15, 18),
            BorderStyle = BorderStyle.FixedSingle,
            AllowDrop = true
        };
        picPreview.Paint += PicPreview_Paint;
        picPreview.DragEnter += PicPreview_DragEnter;
        picPreview.DragDrop += PicPreview_DragDrop;
        this.Controls.Add(picPreview);

        lblFileInfo = new Label
        {
            Text = "Arraste uma imagem ou GIF para a área circular acima",
            Location = new Point(24, 440),
            Size = new Size(340, 45),
            ForeColor = Color.FromArgb(212, 212, 216),
            TextAlign = ContentAlignment.TopCenter
        };
        this.Controls.Add(lblFileInfo);

        // Control Panel (Right Side)
        int rightX = 390;
        int currentY = 90;

        // Load Button
        btnLoad = CreateStyledButton("📂 Selecionar Imagem ou GIF", new Point(rightX, currentY), 380, 42, Color.FromArgb(39, 39, 42));
        btnLoad.Click += BtnLoad_Click;
        this.Controls.Add(btnLoad);
        currentY += 52;

        // Rotation
        var lblRot = new Label
        {
            Text = "Rotação da Tela:",
            Location = new Point(rightX, currentY + 4),
            AutoSize = true
        };
        this.Controls.Add(lblRot);

        cmbRotation = new ComboBox
        {
            Location = new Point(rightX + 150, currentY),
            Width = 230,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(39, 39, 42),
            ForeColor = Color.White
        };
        cmbRotation.Items.AddRange(new object[] { "0° (Padrão)", "90°", "180° (Invertido)", "270°" });
        cmbRotation.SelectedIndex = 0;
        cmbRotation.SelectedIndexChanged += (s, e) => picPreview.Invalidate();
        this.Controls.Add(cmbRotation);
        currentY += 50;

        // Save BIN
        btnSaveBin = CreateStyledButton("💾 Gerar Arquivo .BIN do Water Cooler", new Point(rightX, currentY), 380, 44, Color.FromArgb(234, 88, 12));
        btnSaveBin.Click += BtnSaveBin_Click;
        this.Controls.Add(btnSaveBin);
        currentY += 54;

        // Save BMP
        btnSaveBmp = CreateStyledButton("🖼 Exportar como BMP 16-Bit RGB565", new Point(rightX, currentY), 380, 38, Color.FromArgb(39, 39, 42));
        btnSaveBmp.Click += BtnSaveBmp_Click;
        this.Controls.Add(btnSaveBmp);
        currentY += 60;

        // Separator
        var sep = new Panel
        {
            Location = new Point(rightX, currentY),
            Size = new Size(380, 1),
            BackColor = Color.FromArgb(63, 63, 70)
        };
        this.Controls.Add(sep);
        currentY += 15;

        // Device Detection
        btnDetectCooler = CreateStyledButton("🔍 Detectar Bomba do Cooler (USB/SCSI)", new Point(rightX, currentY), 380, 38, Color.FromArgb(39, 39, 42));
        btnDetectCooler.Click += BtnDetectCooler_Click;
        this.Controls.Add(btnDetectCooler);
        currentY += 44;

        lblDeviceStatus = new Label
        {
            Text = "Status da Bomba: Não verificado",
            Location = new Point(rightX, currentY),
            Size = new Size(380, 24),
            ForeColor = Color.FromArgb(161, 161, 170)
        };
        this.Controls.Add(lblDeviceStatus);
        currentY += 40;

        // GitHub Update Checker
        var lblGh = new Label
        {
            Text = "Repositório GitHub para Atualizações:",
            Location = new Point(rightX, currentY),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F)
        };
        this.Controls.Add(lblGh);
        currentY += 22;

        txtGitHubRepo = new TextBox
        {
            Text = "naustagic/Aorus_Engine-RGB_Fusion",
            Location = new Point(rightX, currentY),
            Width = 260,
            BackColor = Color.FromArgb(39, 39, 42),
            ForeColor = Color.White
        };
        this.Controls.Add(txtGitHubRepo);

        btnCheckUpdate = CreateStyledButton("Verificar", new Point(rightX + 270, currentY - 1), 110, 28, Color.FromArgb(59, 130, 246));
        btnCheckUpdate.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        btnCheckUpdate.Click += BtnCheckUpdate_Click;
        this.Controls.Add(btnCheckUpdate);
        currentY += 32;

        lblUpdateStatus = new Label
        {
            Text = "Versão atual: v3.0.0 (Pronto)",
            Location = new Point(rightX, currentY),
            Size = new Size(380, 22),
            ForeColor = Color.FromArgb(34, 197, 94),
            Font = new Font("Segoe UI", 8.5F)
        };
        this.Controls.Add(lblUpdateStatus);

        // Animation Timer for GIFs
        animationTimer = new System.Windows.Forms.Timer { Interval = 60 };
        animationTimer.Tick += (s, e) =>
        {
            if (loadedFrames.Count > 1)
            {
                currentFrameIndex = (currentFrameIndex + 1) % loadedFrames.Count;
                picPreview.Invalidate();
            }
        };
    }

    private Button CreateStyledButton(string text, Point loc, int w, int h, Color bg)
    {
        return new Button
        {
            Text = text,
            Location = loc,
            Size = new Size(w, h),
            BackColor = bg,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            FlatAppearance = { BorderSize = 0 }
        };
    }

    private void PicPreview_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Draw current frame or placeholder
        if (loadedFrames.Count > 0 && currentFrameIndex < loadedFrames.Count)
        {
            var frame = loadedFrames[currentFrameIndex];
            int rot = cmbRotation.SelectedIndex * 90;
            var opts = new AorusBinOptions { RotationAngle = rot, TargetWidth = 320, TargetHeight = 320 };
            using var prepared = AorusImageProcessor.PrepareFrame(frame, opts);
            g.DrawImage(prepared, new Rectangle(10, 10, 320, 320));
        }
        else
        {
            using var brush = new SolidBrush(Color.FromArgb(20, 20, 25));
            g.FillRectangle(brush, 10, 10, 320, 320);

            var text = "LCD 320x320\nSolte uma imagem ou GIF aqui";
            using var textBrush = new SolidBrush(Color.FromArgb(113, 113, 122));
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, this.Font, textBrush, new RectangleF(10, 10, 320, 320), sf);
        }

        // Draw circular bezel/mask overlay to simulate the AORUS circular screen
        using var pen = new Pen(Color.FromArgb(250, 146, 60), 3f);
        g.DrawEllipse(pen, 10, 10, 320, 320);
    }

    private void PicPreview_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void PicPreview_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            LoadImageFile(files[0]);
        }
    }

    private void BtnLoad_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Imagens Suportadas (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|Todos os arquivos (*.*)|*.*",
            Title = "Selecione uma imagem ou animação GIF"
        };
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            LoadImageFile(ofd.FileName);
        }
    }

    private void LoadImageFile(string path)
    {
        try
        {
            animationTimer.Stop();
            foreach (var f in loadedFrames) f.Dispose();
            loadedFrames.Clear();
            currentFrameIndex = 0;

            using var img = Image.FromFile(path);
            var frameDim = new FrameDimension(img.FrameDimensionsList[0]);
            int count = img.GetFrameCount(frameDim);

            for (int i = 0; i < Math.Min(count, 150); i++)
            {
                img.SelectActiveFrame(frameDim, i);
                loadedFrames.Add(new Bitmap(img));
            }

            currentLoadedPath = path;
            string fname = Path.GetFileName(path);
            lblFileInfo.Text = $"{fname}\n({img.Width}x{img.Height}) — {count} quadro(s)";

            if (count > 1)
            {
                animationTimer.Start();
            }

            picPreview.Invalidate();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar imagem: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnSaveBin_Click(object? sender, EventArgs e)
    {
        if (loadedFrames.Count == 0 || string.IsNullOrEmpty(currentLoadedPath))
        {
            MessageBox.Show("Carregue uma imagem ou GIF primeiro.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Arquivo BIN do AORUS (*.bin)|*.bin",
            FileName = "0.bin",
            Title = "Salvar arquivo de imagem do LCD"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var opts = new AorusBinOptions
                {
                    RotationAngle = cmbRotation.SelectedIndex * 90,
                    TargetWidth = 320,
                    TargetHeight = 320
                };

                byte[] binBytes = AorusImageProcessor.ProcessImageToBin(currentLoadedPath, opts);
                File.WriteAllBytes(sfd.FileName, binBytes);

                MessageBox.Show($"Arquivo BIN gerado com sucesso!\nTamanho: {binBytes.Length / 1024} KB\nQuadros: {loadedFrames.Count}", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao exportar BIN: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void BtnSaveBmp_Click(object? sender, EventArgs e)
    {
        if (loadedFrames.Count == 0)
        {
            MessageBox.Show("Carregue uma imagem primeiro.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Bitmap de 16 bits (*.bmp)|*.bmp",
            FileName = "screen_16bit.bmp",
            Title = "Exportar Bitmap compatível com software antigo"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var opts = new AorusBinOptions { RotationAngle = cmbRotation.SelectedIndex * 90 };
                using var prepared = AorusImageProcessor.PrepareFrame(loadedFrames[0], opts);
                prepared.Save(sfd.FileName, ImageFormat.Bmp);

                MessageBox.Show("Arquivo BMP 16-bit exportado com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar BMP: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void BtnDetectCooler_Click(object? sender, EventArgs e)
    {
        lblDeviceStatus.Text = "Detectando...";
        var dev = AorusScsiTransport.DetectDevice();
        if (dev.IsFound)
        {
            lblDeviceStatus.ForeColor = Color.FromArgb(34, 197, 94);
            lblDeviceStatus.Text = $"Conectado: {dev.ModelName} ({dev.DevicePath})";
        }
        else
        {
            lblDeviceStatus.ForeColor = Color.FromArgb(239, 68, 68);
            lblDeviceStatus.Text = "Dispositivo 'LCD_PLATFORM' não encontrado no barramento USB.";
        }
    }

    private async void BtnCheckUpdate_Click(object? sender, EventArgs e)
    {
        btnCheckUpdate.Enabled = false;
        lblUpdateStatus.Text = "Consultando GitHub Releases...";
        lblUpdateStatus.ForeColor = Color.FromArgb(250, 204, 21);

        string repo = txtGitHubRepo.Text.Trim();
        var info = await AorusImageProcessor.CheckForGitHubUpdatesAsync(repo, "3.0.0");

        if (info.HasUpdate)
        {
            lblUpdateStatus.ForeColor = Color.FromArgb(59, 130, 246);
            lblUpdateStatus.Text = $"🎉 Nova versão disponível: {info.TagName}!";
            var res = MessageBox.Show($"Uma nova versão ({info.TagName}) está disponível no GitHub!\n\nDeseja abrir a página de lançamento no navegador?", "Atualização Encontrada", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (res == DialogResult.Yes && !string.IsNullOrEmpty(info.HtmlUrl))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = info.HtmlUrl,
                    UseShellExecute = true
                });
            }
        }
        else
        {
            lblUpdateStatus.ForeColor = Color.FromArgb(34, 197, 94);
            lblUpdateStatus.Text = "Você já está na versão mais recente (ou repositório offline).";
        }

        btnCheckUpdate.Enabled = true;
    }
}
