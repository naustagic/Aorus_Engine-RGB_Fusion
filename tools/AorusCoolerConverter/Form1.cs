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
    // ── Controls ────────────────────────────────────────────────────────────────
    private PictureBox picPreview = null!;
    private Label lblTitle = null!;
    private Label lblFileInfo = null!;
    private ComboBox cmbTubes = null!;
    private ComboBox cmbRotation = null!;
    private Button btnLoad = null!;
    private Button btnFlashCooler = null!;
    private ProgressBar prgFlash = null!;
    private Label lblFlashStatus = null!;
    private Button btnSaveBin = null!;
    private Button btnSaveBmp = null!;
    private Button btnRestoreDefault = null!;
    private Button btnDetectCooler = null!;
    private Label lblDeviceStatus = null!;
    private TextBox txtGitHubRepo = null!;
    private Button btnCheckUpdate = null!;
    private Label lblUpdateStatus = null!;
    private System.Windows.Forms.Timer animationTimer = null!;

    // ── State ────────────────────────────────────────────────────────────────────
    private List<Bitmap> loadedFrames = new();
    private int currentFrameIndex = 0;
    private string? currentLoadedPath;

    private int GetSelectedTubeAngle()
    {
        return cmbTubes?.SelectedIndex switch
        {
            1 => 0,    // No Topo (12h)
            2 => 270,  // À Esquerda (9h)
            3 => 180,  // Embaixo (6h)
            _ => 90    // À Direita (3h) — padrão do usuário
        };
    }

    private int GetCalculatedBurnAngle()
    {
        int imgAngle = (cmbRotation?.SelectedIndex ?? 0) * 90;
        int tubeAngle = GetSelectedTubeAngle();
        return (imgAngle - tubeAngle + 360) % 360;
    }

    // ── AORUS Orange palette ─────────────────────────────────────────────────────
    private static readonly Color AorusOrange = Color.FromArgb(250, 146, 60);
    private static readonly Color AorusDark   = Color.FromArgb(24, 24, 27);
    private static readonly Color AorusDark2  = Color.FromArgb(39, 39, 42);
    private static readonly Color AorusMid    = Color.FromArgb(63, 63, 70);
    private static readonly Color AorusGray   = Color.FromArgb(161, 161, 170);
    private static readonly Color AorusLight  = Color.FromArgb(244, 244, 245);

    public Form1()
    {
        InitializeComponent();
        SetupCustomUi();
        this.Load += (_, _) => animationTimer.Start();
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // UI BUILD
    // ═════════════════════════════════════════════════════════════════════════════

    private void SetupCustomUi()
    {
        this.Text            = $"AORUS Liquid Cooler — LCD Studio v{AorusImageProcessor.AppVersion}";
        this.Size            = new Size(820, 750);
        this.StartPosition   = FormStartPosition.CenterScreen;
        this.BackColor       = AorusDark;
        this.ForeColor       = AorusLight;
        this.Font            = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox     = false;

        // ── Title ────────────────────────────────────────────────────────────────
        lblTitle = new Label
        {
            Text      = "AORUS Liquid Cooler LCD Studio",
            Font      = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = AorusOrange,
            Location  = new Point(24, 20),
            AutoSize  = true
        };
        this.Controls.Add(lblTitle);

        var lblSub = new Label
        {
            Text      = "Suporta PNG, JPG, BMP e GIF Animado nativamente (sem restrição de 16 bits)",
            Font      = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = AorusGray,
            Location  = new Point(26, 54),
            AutoSize  = true
        };
        this.Controls.Add(lblSub);

        // ── Preview (left) ───────────────────────────────────────────────────────
        picPreview = new PictureBox
        {
            Location    = new Point(24, 90),
            Size        = new Size(340, 340),
            BackColor   = Color.FromArgb(15, 15, 18),
            BorderStyle = BorderStyle.FixedSingle,
            AllowDrop   = true
        };
        picPreview.Paint     += PicPreview_Paint;
        picPreview.DragEnter += PicPreview_DragEnter;
        picPreview.DragDrop  += PicPreview_DragDrop;
        this.Controls.Add(picPreview);

        lblFileInfo = new Label
        {
            Text      = "Arraste uma imagem ou GIF para a área circular acima",
            Location  = new Point(24, 440),
            Size      = new Size(340, 45),
            ForeColor = Color.FromArgb(212, 212, 216),
            TextAlign = ContentAlignment.TopCenter
        };
        this.Controls.Add(lblFileInfo);

        // ── Right panel ──────────────────────────────────────────────────────────
        int rightX   = 390;
        int currentY = 90;

        // Load button
        btnLoad = CreateStyledButton("📂 Selecionar Imagem ou GIF", new Point(rightX, currentY), 380, 42, AorusDark2);
        btnLoad.Click += BtnLoad_Click;
        this.Controls.Add(btnLoad);
        currentY += 46;

        // 1. Posição Física dos Tubos da Bomba no Gabinete (Fixo)
        var lblTubes = new Label
        {
            Text     = "Tubos da Bomba:",
            Location = new Point(rightX, currentY + 6),
            AutoSize = true
        };
        this.Controls.Add(lblTubes);

        cmbTubes = new ComboBox
        {
            Location      = new Point(rightX + 130, currentY),
            Width         = 250,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode      = DrawMode.OwnerDrawFixed,
            ItemHeight    = 24,
            BackColor     = AorusDark2,
            ForeColor     = AorusLight,
            FlatStyle     = FlatStyle.Flat
        };
        cmbTubes.Items.AddRange(new object[] {
            "À Direita (3h) — Padrão",
            "No Topo (12h)",
            "À Esquerda (9h)",
            "Embaixo (6h)"
        });
        cmbTubes.SelectedIndex = 0; // Padrão: À Direita (3h)
        cmbTubes.DrawItem += CmbRotation_DrawItem;
        cmbTubes.SelectedIndexChanged += (s, e) => picPreview.Invalidate();
        this.Controls.Add(cmbTubes);
        currentY += 36;

        // 2. Rotação da Imagem / GIF (Gira apenas a imagem na tela)
        var lblRot = new Label
        {
            Text     = "Girar Imagem:",
            Location = new Point(rightX, currentY + 6),
            AutoSize = true
        };
        this.Controls.Add(lblRot);

        cmbRotation = new ComboBox
        {
            Location      = new Point(rightX + 130, currentY),
            Width         = 250,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode      = DrawMode.OwnerDrawFixed,
            ItemHeight    = 24,
            BackColor     = AorusDark2,
            ForeColor     = AorusLight,
            FlatStyle     = FlatStyle.Flat
        };
        cmbRotation.Items.AddRange(new object[] {
            "0° (Normal)",
            "90° (Girar 90°)",
            "180° (Invertido)",
            "270° (Girar 270°)"
        });
        cmbRotation.SelectedIndex = 0;
        cmbRotation.DrawItem     += CmbRotation_DrawItem;
        cmbRotation.SelectedIndexChanged += (s, e) => picPreview.Invalidate();
        this.Controls.Add(cmbRotation);
        currentY += 46;

        // ── Primary Action: Flash directly to Cooler ─────────────────────────────
        btnFlashCooler = CreateStyledButton("⚡ Gravar no Water Cooler (USB Direto)", new Point(rightX, currentY), 380, 46, Color.FromArgb(234, 88, 12));
        btnFlashCooler.Font  = new Font("Segoe UI", 11F, FontStyle.Bold);
        btnFlashCooler.Click += BtnFlashCooler_Click;
        this.Controls.Add(btnFlashCooler);
        currentY += 52;

        prgFlash = new ProgressBar
        {
            Location = new Point(rightX, currentY),
            Size     = new Size(380, 14),
            Visible  = false
        };
        this.Controls.Add(prgFlash);

        lblFlashStatus = new Label
        {
            Text      = "",
            Location  = new Point(rightX, currentY + 16),
            Size      = new Size(380, 20),
            ForeColor = AorusOrange,
            Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible   = false
        };
        this.Controls.Add(lblFlashStatus);
        currentY += 40;

        // Save BIN & Export BMP (compact side-by-side)
        btnSaveBin = CreateStyledButton("💾 Salvar .BIN", new Point(rightX, currentY), 185, 38, AorusDark2);
        btnSaveBin.Click += BtnSaveBin_Click;
        this.Controls.Add(btnSaveBin);

        btnSaveBmp = CreateStyledButton("🖼 Exportar .BMP", new Point(rightX + 195, currentY), 185, 38, AorusDark2);
        btnSaveBmp.Click += BtnSaveBmp_Click;
        this.Controls.Add(btnSaveBmp);
        currentY += 44;

        // Restore factory animation
        btnRestoreDefault = CreateStyledButton("🔄 Restaurar Padrão de Fábrica (0.bin)", new Point(rightX, currentY), 380, 34, Color.FromArgb(45, 45, 50));
        btnRestoreDefault.Font = new Font("Segoe UI", 9F);
        btnRestoreDefault.Click += BtnRestoreDefault_Click;
        this.Controls.Add(btnRestoreDefault);
        currentY += 46;

        // Separator
        AddSeparator(rightX, currentY, 380);
        currentY += 16;

        // Device Detection
        btnDetectCooler = CreateStyledButton("🔍 Detectar Bomba do Cooler (USB/SCSI)", new Point(rightX, currentY), 380, 38, AorusDark2);
        btnDetectCooler.Click += BtnDetectCooler_Click;
        this.Controls.Add(btnDetectCooler);
        currentY += 44;

        lblDeviceStatus = new Label
        {
            Text      = "Status da Bomba: Não verificado",
            Location  = new Point(rightX, currentY),
            Size      = new Size(380, 24),
            ForeColor = AorusGray
        };
        this.Controls.Add(lblDeviceStatus);
        currentY += 38;

        // Separator
        AddSeparator(rightX, currentY, 380);
        currentY += 16;

        // GitHub Update Checker (repositório bloqueado — somente leitura)
        var lblGh = new Label
        {
            Text     = "Repositório GitHub para Atualizações:",
            Location = new Point(rightX, currentY),
            AutoSize = true,
            Font     = new Font("Segoe UI", 8.5F)
        };
        this.Controls.Add(lblGh);
        currentY += 22;

        txtGitHubRepo = new TextBox
        {
            Text      = "naustagic/Aorus_Engine-RGB_Fusion",
            Location  = new Point(rightX, currentY),
            Width     = 255,
            ReadOnly  = true,                              // <── bloqueado
            BackColor = Color.FromArgb(30, 30, 33),
            ForeColor = AorusGray,
            Font      = new Font("Segoe UI", 9F, FontStyle.Italic),
            BorderStyle = BorderStyle.FixedSingle
        };
        this.Controls.Add(txtGitHubRepo);

        // Lock icon label
        var lblLock = new Label
        {
            Text      = "🔒",
            Location  = new Point(rightX + 260, currentY + 3),
            AutoSize  = true,
            ForeColor = AorusGray
        };
        this.Controls.Add(lblLock);

        btnCheckUpdate = CreateStyledButton("Verificar", new Point(rightX + 285, currentY - 1), 95, 28, Color.FromArgb(59, 130, 246));
        btnCheckUpdate.Font  = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        btnCheckUpdate.Click += BtnCheckUpdate_Click;
        this.Controls.Add(btnCheckUpdate);
        currentY += 32;

        lblUpdateStatus = new Label
        {
            Text      = $"Versão atual: v{AorusImageProcessor.AppVersion} (Pronto)",
            Location  = new Point(rightX, currentY),
            Size      = new Size(380, 22),
            ForeColor = Color.FromArgb(34, 197, 94),
            Font      = new Font("Segoe UI", 8.5F)
        };
        this.Controls.Add(lblUpdateStatus);

        // ── Credits rodapé ───────────────────────────────────────────────────────
        AddCreditsStrip();

        // ── Animation Timer ──────────────────────────────────────────────────────
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

    /// <summary>Rodapé com créditos e link para o repositório.</summary>
    private void AddCreditsStrip()
    {
        var pnlCredits = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 30,
            BackColor = Color.FromArgb(18, 18, 20)
        };

        var lblCredits = new Label
        {
            Text      = "by SaraLapia  •  github.com/naustagic/Aorus_Engine-RGB_Fusion",
            Font      = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = AorusGray,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor    = Cursors.Hand
        };
        lblCredits.Click += (_, _) => OpenUrl("https://github.com/naustagic/Aorus_Engine-RGB_Fusion");

        pnlCredits.Controls.Add(lblCredits);
        this.Controls.Add(pnlCredits);
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // CUSTOM DRAW — ComboBox
    // ═════════════════════════════════════════════════════════════════════════════

    private void CmbRotation_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;

        bool selected = (e.State & DrawItemState.Selected) != 0;

        using var bgBrush = new SolidBrush(selected ? Color.FromArgb(60, 30, 0) : AorusDark2);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        // Orange left bar on selected
        if (selected)
        {
            using var barBrush = new SolidBrush(AorusOrange);
            e.Graphics.FillRectangle(barBrush, e.Bounds.Left, e.Bounds.Top, 3, e.Bounds.Height);
        }

        string text = cmbRotation.Items[e.Index]?.ToString() ?? "";
        using var fgBrush = new SolidBrush(selected ? AorusOrange : AorusLight);
        var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        var textRect = new RectangleF(e.Bounds.Left + 8, e.Bounds.Top, e.Bounds.Width - 8, e.Bounds.Height);
        e.Graphics.DrawString(text, e.Font!, fgBrush, textRect, sf);
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // PREVIEW PAINT — with pump port indicators
    // ═════════════════════════════════════════════════════════════════════════════

    private void PicPreview_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Draw image frame or placeholder
        if (loadedFrames.Count > 0 && currentFrameIndex < loadedFrames.Count)
        {
            var frame = loadedFrames[currentFrameIndex];
            int rot   = cmbRotation.SelectedIndex * 90;
            var opts  = new AorusBinOptions { RotationAngle = rot, TargetWidth = 320, TargetHeight = 320 };
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

        // Circular bezel (orange ring)
        using var bezelPen = new Pen(AorusOrange, 3f);
        g.DrawEllipse(bezelPen, 10, 10, 320, 320);

        // Pump port indicators (rotate with rotation setting)
        DrawPumpPorts(g);
    }

    /// <summary>
    /// Draws two glowing dots on the circular ring representing the physical IN/OUT pump ports.
    /// The ports stay fixed according to the physical tube orientation selected by the user.
    /// </summary>
    private void DrawPumpPorts(Graphics g)
    {
        float cx = 10 + 320 / 2f; // 170
        float cy = 10 + 320 / 2f; // 170
        float r  = 160f;          // ring radius

        float tubeAngle = GetSelectedTubeAngle();

        // IN e OUT posicionados de forma fixa em torno dos tubos
        float a1 = tubeAngle - 25f;
        float a2 = tubeAngle + 25f;

        DrawPort(g, cx, cy, r, a1, "IN",  Color.FromArgb(255, 80, 180, 255));
        DrawPort(g, cx, cy, r, a2, "OUT", Color.FromArgb(255, 255, 160, 60));
    }

    private static void DrawPort(Graphics g, float cx, float cy, float r, float angleDeg, string label, Color portColor)
    {
        double rad = angleDeg * Math.PI / 180.0;
        float px = cx + r * (float)Math.Sin(rad);
        float py = cy - r * (float)Math.Cos(rad);

        // Outer glow
        using var glowBrush = new SolidBrush(Color.FromArgb(60, portColor.R, portColor.G, portColor.B));
        g.FillEllipse(glowBrush, px - 9, py - 9, 18, 18);

        // Inner dot
        using var dotBrush = new SolidBrush(portColor);
        g.FillEllipse(dotBrush, px - 5, py - 5, 10, 10);

        // Ring around dot
        using var dotPen = new Pen(Color.White, 1f);
        g.DrawEllipse(dotPen, px - 5, py - 5, 10, 10);

        // Label (IN / OUT)
        float lx = cx + (r + 14) * (float)Math.Sin(rad);
        float ly = cy - (r + 14) * (float)Math.Cos(rad);
        using var lblBrush = new SolidBrush(portColor);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(label, new Font("Segoe UI", 7F, FontStyle.Bold), lblBrush,
                     new RectangleF(lx - 16, ly - 8, 32, 16), sf);
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═════════════════════════════════════════════════════════════════════════════

    private Button CreateStyledButton(string text, Point loc, int w, int h, Color bg)
    {
        return new Button
        {
            Text        = text,
            Location    = loc,
            Size        = new Size(w, h),
            BackColor   = bg,
            ForeColor   = Color.White,
            FlatStyle   = FlatStyle.Flat,
            Cursor      = Cursors.Hand,
            FlatAppearance = { BorderSize = 0 }
        };
    }

    private void AddSeparator(int x, int y, int width)
    {
        var sep = new Panel
        {
            Location  = new Point(x, y),
            Size      = new Size(width, 1),
            BackColor = AorusMid
        };
        this.Controls.Add(sep);
    }

    private static void OpenUrl(string url)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = url,
            UseShellExecute = true
        });
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // DRAG & DROP
    // ═════════════════════════════════════════════════════════════════════════════

    private void PicPreview_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void PicPreview_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            LoadImageFile(files[0]);
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // IMAGE LOADING
    // ═════════════════════════════════════════════════════════════════════════════

    private void BtnLoad_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Imagens Suportadas (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|Todos os arquivos (*.*)|*.*",
            Title  = "Selecione uma imagem ou animação GIF"
        };
        if (ofd.ShowDialog() == DialogResult.OK)
            LoadImageFile(ofd.FileName);
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
            int count    = img.GetFrameCount(frameDim);
            int maxFrames = 50;
            int step = 1;
            if (count > maxFrames)
            {
                step = (int)Math.Ceiling((double)count / maxFrames);
            }

            using var canvas = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
            using var gCanvas = Graphics.FromImage(canvas);
            gCanvas.Clear(Color.Black);

            for (int i = 0; i < count; i += step)
            {
                if (loadedFrames.Count >= maxFrames) break;
                img.SelectActiveFrame(frameDim, i);
                gCanvas.DrawImage(img, 0, 0, img.Width, img.Height);
                loadedFrames.Add(new Bitmap(canvas));
            }

            currentLoadedPath = path;
            string fname = Path.GetFileName(path);
            lblFileInfo.Text = $"{fname}\n({img.Width}x{img.Height}) — {count} quadros ({loadedFrames.Count} convertidos para LCD)";

            if (count > 1)
                animationTimer.Start();

            picPreview.Invalidate();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar imagem: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // SAVE
    // ═════════════════════════════════════════════════════════════════════════════

    private void BtnSaveBin_Click(object? sender, EventArgs e)
    {
        if (loadedFrames.Count == 0 || string.IsNullOrEmpty(currentLoadedPath))
        {
            MessageBox.Show("Carregue uma imagem ou GIF primeiro.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter   = "Arquivo BIN do AORUS (*.bin)|*.bin",
            FileName = "0.bin",
            Title    = "Salvar arquivo de imagem do LCD"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var opts = new AorusBinOptions
                {
                    RotationAngle = GetCalculatedBurnAngle(),
                    TargetWidth   = 320,
                    TargetHeight  = 320
                };

                byte[] binBytes = AorusImageProcessor.ProcessImageToBin(currentLoadedPath, opts);
                File.WriteAllBytes(sfd.FileName, binBytes);

                MessageBox.Show(
                    $"Arquivo BIN gerado com sucesso!\nTamanho: {binBytes.Length / 1024} KB\nQuadros: {loadedFrames.Count}",
                    "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            Filter   = "Bitmap de 16 bits (*.bmp)|*.bmp",
            FileName = "screen_16bit.bmp",
            Title    = "Exportar Bitmap compatível com software antigo"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var opts = new AorusBinOptions { RotationAngle = GetCalculatedBurnAngle() };
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

    // ═════════════════════════════════════════════════════════════════════════════
    // FLASH DIRECTLY TO COOLER
    // ═════════════════════════════════════════════════════════════════════════════

    private async void BtnFlashCooler_Click(object? sender, EventArgs e)
    {
        if (loadedFrames.Count == 0 || string.IsNullOrEmpty(currentLoadedPath))
        {
            MessageBox.Show("Por favor, selecione uma imagem ou GIF animado primeiro.",
                "Nenhuma Imagem Selecionada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            BtnLoad_Click(sender, e);
            if (loadedFrames.Count == 0 || string.IsNullOrEmpty(currentLoadedPath)) return;
        }

        var dr = MessageBox.Show(
            $"Deseja gravar a animação/imagem exibida ({loadedFrames.Count} frame(s)) diretamente no LCD do Water Cooler via USB?\n\n" +
            "A ferramenta preparará o payload na rotação selecionada e gravará na memória Flash SPI.",
            "Confirmar Gravação no Water Cooler",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (dr != DialogResult.Yes) return;

        btnFlashCooler.Enabled  = false;
        btnSaveBin.Enabled      = false;
        btnSaveBmp.Enabled      = false;
        btnLoad.Enabled         = false;
        btnDetectCooler.Enabled = false;

        prgFlash.Visible        = true;
        prgFlash.Value          = 5;
        lblFlashStatus.Visible  = true;
        lblFlashStatus.Text     = "Preparando payload da imagem...";
        lblFlashStatus.ForeColor= AorusOrange;

        try
        {
            var opt = new AorusBinOptions
            {
                RotationAngle = GetCalculatedBurnAngle()
            };

            byte[] binData = await Task.Run(() => AorusImageProcessor.ProcessImageToBin(currentLoadedPath, opt));

            var prog = new Progress<(int Percent, string Status)>(report =>
            {
                prgFlash.Value = Math.Clamp(report.Percent, 0, 100);
                lblFlashStatus.Text = report.Status;
            });

            var result = await Task.Run(() => AorusScsiTransport.FlashPayloadToCoolerAsync(binData, prog));

            if (result.Success)
            {
                lblFlashStatus.ForeColor = Color.FromArgb(34, 197, 94);
                lblFlashStatus.Text      = "✓ Gravação concluída com sucesso!";
                MessageBox.Show(
                    "🎉 Gravação Concluída com Sucesso!\n\n" +
                    "A nova imagem/animação foi gravada na memória Flash do Water Cooler e já está ativa no LCD.",
                    "Sucesso!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                lblFlashStatus.ForeColor = Color.FromArgb(239, 68, 68);
                lblFlashStatus.Text      = "Falha na gravação.";
                MessageBox.Show(result.Message, "Falha na Gravação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            lblFlashStatus.ForeColor = Color.FromArgb(239, 68, 68);
            lblFlashStatus.Text      = "Erro inesperado.";
            MessageBox.Show($"Ocorreu um erro ao processar ou gravar:\n{ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnFlashCooler.Enabled  = true;
            btnSaveBin.Enabled      = true;
            btnSaveBmp.Enabled      = true;
            btnRestoreDefault.Enabled = true;
            btnLoad.Enabled         = true;
            btnDetectCooler.Enabled = true;
        }
    }

    private async void BtnRestoreDefault_Click(object? sender, EventArgs e)
    {
        var dr = MessageBox.Show(
            "Deseja restaurar o Water Cooler para a animação original de fábrica (0.bin)?\n\n" +
            "Isso apagará o chuvisco/ruído e reinstalará a animação oficial completa da Gigabyte Aorus.",
            "Restaurar Padrão de Fábrica",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (dr != DialogResult.Yes) return;

        string[] potentialPaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "0.bin"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "aorus_engine", "extracted", "app", "AIO", "images", "0.bin"),
            @"d:\eng-rev-watercooler\aorus_engine\extracted\app\AIO\images\0.bin"
        };

        string? binPath = potentialPaths.FirstOrDefault(File.Exists);
        if (binPath == null)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Localize o arquivo 0.bin original da Gigabyte",
                Filter = "Arquivo BIN (*.bin)|*.bin|Todos os arquivos (*.*)|*.*"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                binPath = ofd.FileName;
            }
            else return;
        }

        btnFlashCooler.Enabled    = false;
        btnSaveBin.Enabled        = false;
        btnSaveBmp.Enabled        = false;
        btnRestoreDefault.Enabled = false;
        btnLoad.Enabled           = false;
        btnDetectCooler.Enabled   = false;

        prgFlash.Visible          = true;
        prgFlash.Value            = 5;
        lblFlashStatus.Visible    = true;
        lblFlashStatus.Text       = "Carregando 0.bin de fábrica...";
        lblFlashStatus.ForeColor  = AorusOrange;

        try
        {
            byte[] binData = await File.ReadAllBytesAsync(binPath);
            var prog = new Progress<(int Percent, string Status)>(report =>
            {
                prgFlash.Value = Math.Clamp(report.Percent, 0, 100);
                lblFlashStatus.Text = report.Status;
            });

            var result = await Task.Run(() => AorusScsiTransport.FlashPayloadToCoolerAsync(binData, prog));

            if (result.Success)
            {
                lblFlashStatus.ForeColor = Color.FromArgb(34, 197, 94);
                lblFlashStatus.Text      = "✓ Padrão de fábrica restaurado!";
                MessageBox.Show(
                    "🎉 Padrão de Fábrica Restaurado com Sucesso!\n\n" +
                    "A animação oficial Aorus foi regravada na memória Flash e o display voltou ao normal.",
                    "Sucesso!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                lblFlashStatus.ForeColor = Color.FromArgb(239, 68, 68);
                lblFlashStatus.Text      = "Falha ao restaurar.";
                MessageBox.Show(result.Message, "Falha na Gravação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            lblFlashStatus.ForeColor = Color.FromArgb(239, 68, 68);
            lblFlashStatus.Text      = "Erro inesperado.";
            MessageBox.Show($"Erro ao restaurar: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnFlashCooler.Enabled    = true;
            btnSaveBin.Enabled        = true;
            btnSaveBmp.Enabled        = true;
            btnRestoreDefault.Enabled = true;
            btnLoad.Enabled           = true;
            btnDetectCooler.Enabled   = true;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // DEVICE DETECTION — tenta ler rotação atual do dispositivo
    // ═════════════════════════════════════════════════════════════════════════════

    private void BtnDetectCooler_Click(object? sender, EventArgs e)
    {
        lblDeviceStatus.Text      = "Detectando...";
        lblDeviceStatus.ForeColor = AorusGray;

        var dev = AorusScsiTransport.DetectDevice();
        if (dev.IsFound)
        {
            lblDeviceStatus.ForeColor = Color.FromArgb(34, 197, 94);
            lblDeviceStatus.Text      = $"Conectado: {dev.ModelName}";

            // Tentar ler rotação atual do dispositivo
            int detectedRot = TryReadDeviceRotation(dev.DevicePath);
            if (detectedRot >= 0)
            {
                int idx = detectedRot / 90;
                if (idx >= 0 && idx < cmbRotation.Items.Count)
                {
                    cmbRotation.SelectedIndex = idx;
                    lblDeviceStatus.Text     += $"  |  Rotação: {detectedRot}°";
                }
            }
        }
        else
        {
            lblDeviceStatus.ForeColor = Color.FromArgb(239, 68, 68);
            lblDeviceStatus.Text      = "Dispositivo 'LCD_PLATFORM' não encontrado.";
        }
    }

    /// <summary>
    /// Tenta consultar a rotação atual armazenada no firmware do dispositivo.
    /// Retorna 0, 90, 180 ou 270 se bem-sucedido; -1 se não for possível determinar.
    /// </summary>
    private static int TryReadDeviceRotation(string devicePath)
    {
        try
        {
            using var transport = new AorusScsiTransport();
            if (!transport.OpenDevice(devicePath)) return -1;

            // CMD 0xF9 — leitura de parâmetros de configuração (especulativo)
            var buf = new byte[64];
            bool ok = transport.SendScsiCommand(0xF9, 0x80 | 0x40, 0, (uint)buf.Length, buf);

            if (ok && buf.Length >= 2)
            {
                int rotByte = buf[1] & 0x03; // bits [1:0] = rotação index
                return rotByte * 90;
            }
        }
        catch { /* silencioso — dispositivo pode não suportar este comando */ }

        return -1; // não determinado
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // UPDATE CHECKER
    // ═════════════════════════════════════════════════════════════════════════════

    private async void BtnCheckUpdate_Click(object? sender, EventArgs e)
    {
        btnCheckUpdate.Enabled = false;
        lblUpdateStatus.Text      = "Consultando GitHub Releases...";
        lblUpdateStatus.ForeColor = Color.FromArgb(250, 204, 21);

        string repo = txtGitHubRepo.Text.Trim();
        var info    = await AorusImageProcessor.CheckForGitHubUpdatesAsync(repo);

        if (info.HasUpdate)
        {
            lblUpdateStatus.ForeColor = Color.FromArgb(59, 130, 246);
            lblUpdateStatus.Text      = $"🎉 Nova versão disponível: {info.TagName}!";
            var res = MessageBox.Show(
                $"Nova versão ({info.TagName}) disponível!\nDeseja abrir a página de lançamento?",
                "Atualização Encontrada", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (res == DialogResult.Yes && !string.IsNullOrEmpty(info.HtmlUrl))
                OpenUrl(info.HtmlUrl);
        }
        else
        {
            lblUpdateStatus.ForeColor = Color.FromArgb(34, 197, 94);
            lblUpdateStatus.Text      = $"✓ Você já está na versão mais recente (v{AorusImageProcessor.AppVersion}).";
            MessageBox.Show(
                $"Você já está utilizando a versão mais recente (v{AorusImageProcessor.AppVersion})!",
                "Sem Atualizações", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        btnCheckUpdate.Enabled = true;
    }
}
