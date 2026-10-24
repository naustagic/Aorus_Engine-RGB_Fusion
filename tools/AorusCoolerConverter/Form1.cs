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
    private CheckBox chkDryRun = null!;
    private Button btnFlashCooler = null!;
    private ProgressBar prgFlash = null!;
    private Label lblFlashStatus = null!;
    private Button btnDumpFlash = null!;
    private Button btnSaveBin = null!;
    private Button btnSaveBmp = null!;
    private Button btnRestoreDefault = null!;
    private Button btnCloseGigabyte = null!;
    private Button btnDetectCooler = null!;
    private Label lblDeviceStatus = null!;
    private TextBox txtGitHubRepo = null!;
    private Button btnCheckUpdate = null!;
    private Label lblUpdateStatus = null!;
    private System.Windows.Forms.Timer animationTimer = null!;

    // ── Debug Panel Controls ─────────────────────────────────────────────────────
    private TextBox txtDebugLog = null!;
    private Button btnOpenLogFile = null!;
    private Button btnClearLog = null!;

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
        this.Load += (_, _) =>
        {
            animationTimer.Start();
            AorusLogger.OnLogMessage += AppendDebugLog;
            AorusLogger.Log("APP", $"AORUS Liquid Cooler LCD Studio v{AorusImageProcessor.AppVersion} iniciado.");
            BtnDetectCooler_Click(this, EventArgs.Empty);
        };
        this.FormClosed += (_, _) =>
        {
            AorusLogger.OnLogMessage -= AppendDebugLog;
        };
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // UI BUILD
    // ═════════════════════════════════════════════════════════════════════════════

    private void SetupCustomUi()
    {
        this.Text            = $"AORUS Liquid Cooler — LCD Studio v{AorusImageProcessor.AppVersion} [Diagnóstico & Dry Run]";
        this.Size            = new Size(1220, 780);
        this.StartPosition   = FormStartPosition.CenterScreen;
        this.BackColor       = AorusDark;
        this.ForeColor       = AorusLight;
        this.Font            = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox     = false;

        // ── Title ────────────────────────────────────────────────────────────────
        lblTitle = new Label
        {
            Text      = "AORUS Liquid Cooler LCD Studio",
            Font      = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = AorusOrange,
            Location  = new Point(24, 18),
            AutoSize  = true
        };
        this.Controls.Add(lblTitle);

        var lblSub = new Label
        {
            Text      = "Módulo de Diagnóstico, Reconexão IAP e Validação com Dry Run Seguro",
            Font      = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = AorusGray,
            Location  = new Point(26, 50),
            AutoSize  = true
        };
        this.Controls.Add(lblSub);

        // ── Preview (left, X=24) ─────────────────────────────────────────────────
        picPreview = new PictureBox
        {
            Location    = new Point(24, 82),
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
            Location  = new Point(24, 430),
            Size      = new Size(340, 40),
            ForeColor = Color.FromArgb(212, 212, 216),
            TextAlign = ContentAlignment.TopCenter
        };
        this.Controls.Add(lblFileInfo);

        // Banner de Proteção contra Formatação do Windows (Tarefa 8)
        var pnlFormatWarning = new Panel
        {
            Location    = new Point(24, 480),
            Size        = new Size(340, 90),
            BackColor   = Color.FromArgb(45, 30, 15),
            BorderStyle = BorderStyle.FixedSingle
        };
        var lblFormatWarnTitle = new Label
        {
            Text      = "⚠️ AVISO DE SEGURANÇA WINDOWS:",
            Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(251, 191, 36),
            Location  = new Point(8, 6),
            AutoSize  = true
        };
        pnlFormatWarning.Controls.Add(lblFormatWarnTitle);

        var lblFormatWarnBody = new Label
        {
            Text      = "Se o Windows abrir uma janela com a mensagem 'Você precisa formatar o disco na unidade antes de usá-lo', clique SEMPRE em CANCELAR. O cooler não usa NTFS/FAT.",
            Font      = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(254, 243, 199),
            Location  = new Point(8, 28),
            Size      = new Size(322, 56)
        };
        pnlFormatWarning.Controls.Add(lblFormatWarnBody);
        this.Controls.Add(pnlFormatWarning);

        // ── Middle Panel (X=390) ─────────────────────────────────────────────────
        int midX     = 390;
        int currentY = 82;

        // Load button
        btnLoad = CreateStyledButton("📂 Selecionar Imagem ou GIF", new Point(midX, currentY), 360, 38, AorusDark2);
        btnLoad.Click += BtnLoad_Click;
        this.Controls.Add(btnLoad);
        currentY += 44;

        // Tubos da Bomba (Posição física fixa)
        var lblTubes = new Label
        {
            Text     = "Tubos da Bomba:",
            Location = new Point(midX, currentY + 4),
            AutoSize = true
        };
        this.Controls.Add(lblTubes);

        cmbTubes = new ComboBox
        {
            Location      = new Point(midX + 120, currentY),
            Width         = 240,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode      = DrawMode.OwnerDrawFixed,
            ItemHeight    = 22,
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
        cmbTubes.SelectedIndex = 0;
        cmbTubes.DrawItem += CmbRotation_DrawItem;
        cmbTubes.SelectedIndexChanged += (s, e) => picPreview.Invalidate();
        this.Controls.Add(cmbTubes);
        currentY += 34;

        // Girar Imagem
        var lblRot = new Label
        {
            Text     = "Girar Imagem:",
            Location = new Point(midX, currentY + 4),
            AutoSize = true
        };
        this.Controls.Add(lblRot);

        cmbRotation = new ComboBox
        {
            Location      = new Point(midX + 120, currentY),
            Width         = 240,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode      = DrawMode.OwnerDrawFixed,
            ItemHeight    = 22,
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
        currentY += 40;

        // Caixa Modo Simulação (Dry Run) — Tarefa 7
        chkDryRun = new CheckBox
        {
            Text      = "Modo Simulação (Dry Run) — Sem escrita na Flash",
            Location  = new Point(midX, currentY),
            Size      = new Size(360, 24),
            Checked   = true, // Padrão ATIVO por segurança
            ForeColor = Color.FromArgb(52, 211, 153),
            Font      = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        chkDryRun.CheckedChanged += (s, e) =>
        {
            if (chkDryRun.Checked)
            {
                btnFlashCooler.Text = "⚡ Executar Simulação / Dry Run";
                btnFlashCooler.BackColor = Color.FromArgb(16, 185, 129); // Verde
            }
            else
            {
                btnFlashCooler.Text = "⚠️ Gravar no Water Cooler (Flash Real)";
                btnFlashCooler.BackColor = Color.FromArgb(234, 88, 12); // Laranja
            }
        };
        this.Controls.Add(chkDryRun);
        currentY += 28;

        // Botão Principal: Dry Run / Flash
        btnFlashCooler = CreateStyledButton("⚡ Executar Simulação / Dry Run", new Point(midX, currentY), 360, 42, Color.FromArgb(16, 185, 129));
        btnFlashCooler.Font  = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        btnFlashCooler.Click += BtnFlashCooler_Click;
        this.Controls.Add(btnFlashCooler);
        currentY += 48;

        prgFlash = new ProgressBar
        {
            Location = new Point(midX, currentY),
            Size     = new Size(360, 12),
            Visible  = false
        };
        this.Controls.Add(prgFlash);

        lblFlashStatus = new Label
        {
            Text      = "",
            Location  = new Point(midX, currentY + 14),
            Size      = new Size(360, 18),
            ForeColor = AorusOrange,
            Font      = new Font("Segoe UI", 8F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible   = false
        };
        this.Controls.Add(lblFlashStatus);
        currentY += 36;

        // Botão Salvar Dump da Flash (Tarefa 9)
        btnDumpFlash = CreateStyledButton("📥 Salvar Dump da Flash (Somente Leitura - SHA-256)", new Point(midX, currentY), 360, 34, Color.FromArgb(30, 58, 138));
        btnDumpFlash.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        btnDumpFlash.Click += BtnDumpFlash_Click;
        this.Controls.Add(btnDumpFlash);
        currentY += 40;

        // Save BIN & Export BMP
        btnSaveBin = CreateStyledButton("💾 Salvar .BIN", new Point(midX, currentY), 175, 32, AorusDark2);
        btnSaveBin.Click += BtnSaveBin_Click;
        this.Controls.Add(btnSaveBin);

        btnSaveBmp = CreateStyledButton("🖼 Exportar .BMP", new Point(midX + 185, currentY), 175, 32, AorusDark2);
        btnSaveBmp.Click += BtnSaveBmp_Click;
        this.Controls.Add(btnSaveBmp);
        currentY += 38;

        // Botão Restaurar Padrão de Fábrica — DESATIVADO (Tarefa 10)
        btnRestoreDefault = CreateStyledButton("🔒 Restaurar Padrão de Fábrica (0.bin) [Desativado]", new Point(midX, currentY), 360, 32, Color.FromArgb(35, 35, 40));
        btnRestoreDefault.Font = new Font("Segoe UI", 8.5F);
        btnRestoreDefault.ForeColor = Color.FromArgb(120, 120, 130);
        btnRestoreDefault.Enabled = false; // DESATIVADO CONFORME TAREFA 10
        btnRestoreDefault.Click += (s, e) =>
        {
            MessageBox.Show(
                "O botão de restauração (0.bin) foi temporariamente desativado a seu pedido para revisão e validação do fluxo IAP.\n" +
                "Ele só será reativado após sua aprovação.",
                "Funcionalidade Desativada para Testes",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        };
        this.Controls.Add(btnRestoreDefault);
        currentY += 40;

        AddSeparator(midX, currentY, 360);
        currentY += 12;

        // Botão Encerrar Processos Gigabyte (Tarefa 5)
        btnCloseGigabyte = CreateStyledButton("🛡️ Fechar Processos Gigabyte Concorrentes", new Point(midX, currentY), 360, 32, Color.FromArgb(127, 29, 29));
        btnCloseGigabyte.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        btnCloseGigabyte.Click += BtnCloseGigabyte_Click;
        this.Controls.Add(btnCloseGigabyte);
        currentY += 38;

        // Botão Detecção / Diagnóstico
        btnDetectCooler = CreateStyledButton("🔍 Diagnóstico USB / Hardware", new Point(midX, currentY), 360, 32, AorusDark2);
        btnDetectCooler.Click += BtnDetectCooler_Click;
        this.Controls.Add(btnDetectCooler);
        currentY += 36;

        lblDeviceStatus = new Label
        {
            Text      = "Status: Não verificado",
            Location  = new Point(midX, currentY),
            Size      = new Size(360, 36),
            ForeColor = AorusGray,
            Font      = new Font("Segoe UI", 8.5F)
        };
        this.Controls.Add(lblDeviceStatus);
        currentY += 42;

        AddSeparator(midX, currentY, 360);
        currentY += 10;

        // GitHub Repo
        var lblGh = new Label
        {
            Text     = "Repositório GitHub:",
            Location = new Point(midX, currentY),
            AutoSize = true,
            Font     = new Font("Segoe UI", 8F)
        };
        this.Controls.Add(lblGh);

        txtGitHubRepo = new TextBox
        {
            Text      = "naustagic/Aorus_Engine-RGB_Fusion",
            Location  = new Point(midX + 115, currentY - 2),
            Width     = 180,
            ReadOnly  = true,
            BackColor = Color.FromArgb(30, 30, 33),
            ForeColor = AorusGray,
            Font      = new Font("Segoe UI", 8.5F),
            BorderStyle = BorderStyle.FixedSingle
        };
        this.Controls.Add(txtGitHubRepo);

        btnCheckUpdate = CreateStyledButton("Verificar", new Point(midX + 300, currentY - 3), 60, 24, Color.FromArgb(37, 99, 235));
        btnCheckUpdate.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        btnCheckUpdate.Click += BtnCheckUpdate_Click;
        this.Controls.Add(btnCheckUpdate);
        currentY += 24;

        lblUpdateStatus = new Label
        {
            Text      = $"Versão atual: v{AorusImageProcessor.AppVersion}",
            Location  = new Point(midX, currentY),
            Size      = new Size(360, 18),
            ForeColor = Color.FromArgb(34, 197, 94),
            Font      = new Font("Segoe UI", 8F)
        };
        this.Controls.Add(lblUpdateStatus);

        // ── Right Panel: Real-Time Debug Console (X=775, W=415) ───────────────────
        int rightX = 775;
        int rightW = 415;

        var lblDebugTitle = new Label
        {
            Text      = "Console de Diagnóstico & Debug (Tempo Real)",
            Location  = new Point(rightX, 20),
            Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = AorusOrange,
            AutoSize  = true
        };
        this.Controls.Add(lblDebugTitle);

        // Caixa de Instruções da Ordem de Teste
        var pnlTestOrder = new Panel
        {
            Location    = new Point(rightX, 48),
            Size        = new Size(rightW, 85),
            BackColor   = Color.FromArgb(20, 25, 35),
            BorderStyle = BorderStyle.FixedSingle
        };
        var lblOrderTitle = new Label
        {
            Text      = "📋 ORDEM DE TESTE RECOMENDADA:",
            Font      = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(96, 165, 250),
            Location  = new Point(6, 4),
            AutoSize  = true
        };
        pnlTestOrder.Controls.Add(lblOrderTitle);

        var lblOrderBody = new Label
        {
            Text      = "1. Clicar em 'Fechar Processos Gigabyte' (ou fechar RGBFusion/Aorus Engine).\n2. No PowerShell: Get-PnpDevice -PresentOnly | Where-Object InstanceId -match 'VID_04D9'\n3. Com 'Modo Simulação (Dry Run)' marcado, clicar em Executar e enviar o log.",
            Font      = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(226, 232, 240),
            Location  = new Point(6, 24),
            Size      = new Size(rightW - 12, 56)
        };
        pnlTestOrder.Controls.Add(lblOrderBody);
        this.Controls.Add(pnlTestOrder);

        // Text console
        txtDebugLog = new TextBox
        {
            Location    = new Point(rightX, 140),
            Size        = new Size(rightW, 545),
            Multiline   = true,
            ReadOnly    = true,
            ScrollBars  = ScrollBars.Both,
            BackColor   = Color.FromArgb(10, 10, 12),
            ForeColor   = Color.FromArgb(228, 228, 231),
            Font        = new Font("Consolas", 8.5F, FontStyle.Regular),
            BorderStyle = BorderStyle.FixedSingle
        };
        this.Controls.Add(txtDebugLog);

        btnOpenLogFile = CreateStyledButton("📂 Abrir aorus_flash_debug.log", new Point(rightX, 695), 260, 32, AorusDark2);
        btnOpenLogFile.Font = new Font("Segoe UI", 8.5F);
        btnOpenLogFile.Click += (s, e) =>
        {
            string path = AorusLogger.GetLogPath();
            if (File.Exists(path))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            else
            {
                MessageBox.Show("Arquivo de log ainda não foi criado.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        this.Controls.Add(btnOpenLogFile);

        btnClearLog = CreateStyledButton("🧹 Limpar Console", new Point(rightX + 270, 695), 145, 32, AorusDark2);
        btnClearLog.Font = new Font("Segoe UI", 8.5F);
        btnClearLog.Click += (s, e) => txtDebugLog.Clear();
        this.Controls.Add(btnClearLog);

        // Timer de animação de preview
        animationTimer = new System.Windows.Forms.Timer { Interval = 100 };
        animationTimer.Tick += AnimationTimer_Tick;
    }

    private void AppendDebugLog(string formattedMessage)
    {
        if (txtDebugLog == null || txtDebugLog.IsDisposed) return;

        if (txtDebugLog.InvokeRequired)
        {
            txtDebugLog.BeginInvoke(new Action(() => AppendDebugLog(formattedMessage)));
            return;
        }

        txtDebugLog.AppendText(formattedMessage + Environment.NewLine);
        txtDebugLog.SelectionStart = txtDebugLog.TextLength;
        txtDebugLog.ScrollToCaret();
    }

    private static Button CreateStyledButton(string text, Point loc, int w, int h, Color bg)
    {
        return new Button
        {
            Text      = text,
            Location  = loc,
            Size      = new Size(w, h),
            BackColor = bg,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
            FlatAppearance = { BorderSize = 0 }
        };
    }

    private static void CmbRotation_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || sender is not ComboBox cb) return;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using var b = new SolidBrush(sel ? AorusOrange : AorusDark2);
        e.Graphics.FillRectangle(b, e.Bounds);
        using var tb = new SolidBrush(sel ? Color.Black : AorusLight);
        e.Graphics.DrawString(cb.Items[e.Index]?.ToString() ?? "", e.Font!, tb, e.Bounds.X + 6, e.Bounds.Y + 3);
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        if (loadedFrames.Count > 1)
        {
            currentFrameIndex = (currentFrameIndex + 1) % loadedFrames.Count;
            picPreview.Invalidate();
        }
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
            currentLoadedPath = path;
            foreach (var f in loadedFrames) f.Dispose();
            loadedFrames.Clear();
            currentFrameIndex = 0;

            using var original = Image.FromFile(path);
            var frameDim = new FrameDimension(original.FrameDimensionsList[0]);
            int count    = original.GetFrameCount(frameDim);

            int maxF = Math.Min(count, 50);
            int step = (count > maxF) ? (int)Math.Ceiling((double)count / maxF) : 1;

            using var canvas = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
            using var gCanvas = Graphics.FromImage(canvas);
            gCanvas.Clear(Color.Black);

            for (int i = 0; i < count; i += step)
            {
                if (loadedFrames.Count >= maxF) break;
                original.SelectActiveFrame(frameDim, i);
                gCanvas.DrawImage(original, 0, 0, original.Width, original.Height);

                var bmpClone = new Bitmap(canvas);
                loadedFrames.Add(bmpClone);
            }

            lblFileInfo.Text = $"{Path.GetFileName(path)}\n({original.Width}x{original.Height}) — {count} quadros ({loadedFrames.Count} convertidos para LCD)";
            picPreview.Invalidate();

            AorusLogger.Log("IMG", $"Imagem/GIF carregado: '{Path.GetFileName(path)}' ({original.Width}x{original.Height}), {loadedFrames.Count} frame(s).");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar imagem: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // PREVIEW PAINT
    // ═════════════════════════════════════════════════════════════════════════════

    private void PicPreview_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (loadedFrames.Count > 0 && currentFrameIndex < loadedFrames.Count)
        {
            var frame = loadedFrames[currentFrameIndex];
            int imgRot = cmbRotation.SelectedIndex * 90;
            var opts  = new AorusBinOptions { RotationAngle = imgRot, TargetWidth = 320, TargetHeight = 320 };
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

        DrawPumpPorts(g);
    }

    private void DrawPumpPorts(Graphics g)
    {
        float cx = 10 + 320 / 2f;
        float cy = 10 + 320 / 2f;
        float r  = 160f;

        float tubeAngle = GetSelectedTubeAngle();

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

        using var glowBrush = new SolidBrush(Color.FromArgb(60, portColor.R, portColor.G, portColor.B));
        g.FillEllipse(glowBrush, px - 9, py - 9, 18, 18);

        using var dotBrush = new SolidBrush(portColor);
        g.FillEllipse(dotBrush, px - 4, py - 4, 8, 8);

        float lx = px + 14 * (float)Math.Sin(rad);
        float ly = py - 14 * (float)Math.Cos(rad);

        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var font = new Font("Segoe UI", 7F, FontStyle.Bold);
        using var textBrush = new SolidBrush(portColor);
        g.DrawString(label, font, textBrush, lx, ly, sf);
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // EXPORT
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
    // FLASH DIRECTLY TO COOLER / DRY RUN (Tarefas 1, 4, 7 e 8)
    // ═════════════════════════════════════════════════════════════════════════════

    private async void BtnFlashCooler_Click(object? sender, EventArgs e)
    {
        bool isDryRun = chkDryRun.Checked;

        if (isDryRun)
        {
            AorusLogger.Log("USER", "Executando em Modo Simulação (Dry Run). Nenhuma escrita na Flash será feita.");
        }
        else
        {
            var dr = MessageBox.Show(
                "ATENÇÃO: O Modo Simulação (Dry Run) está DESMARCADO.\n\n" +
                "Você está prestes a autorizar gravação física na memória Flash do Water Cooler.\n\n" +
                "Recomenda-se realizar o Dry Run primeiro antes de qualquer gravação real.\n\n" +
                "Deseja prosseguir com a gravação real?",
                "Confirmar Gravação Real",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (dr != DialogResult.Yes) return;
        }

        btnFlashCooler.Enabled    = false;
        btnSaveBin.Enabled        = false;
        btnSaveBmp.Enabled        = false;
        btnLoad.Enabled           = false;
        btnDumpFlash.Enabled      = false;
        btnDetectCooler.Enabled   = false;
        btnCloseGigabyte.Enabled  = false;

        prgFlash.Visible          = true;
        prgFlash.Value            = 5;
        lblFlashStatus.Visible    = true;
        lblFlashStatus.Text       = isDryRun ? "Executando Simulação (Dry Run)..." : "Preparando payload da imagem...";
        lblFlashStatus.ForeColor  = AorusOrange;

        try
        {
            byte[] binData = Array.Empty<byte>();
            if (loadedFrames.Count > 0 && !string.IsNullOrEmpty(currentLoadedPath))
            {
                var opt = new AorusBinOptions { RotationAngle = GetCalculatedBurnAngle() };
                binData = await Task.Run(() => AorusImageProcessor.ProcessImageToBin(currentLoadedPath, opt));
            }
            else if (!isDryRun)
            {
                MessageBox.Show("Selecione uma imagem ou GIF antes de realizar a gravação real.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var prog = new Progress<(int Percent, string Status)>(report =>
            {
                prgFlash.Value = Math.Clamp(report.Percent, 0, 100);
                lblFlashStatus.Text = report.Status;
            });

            var result = await Task.Run(() => AorusScsiTransport.FlashPayloadToCoolerAsync(binData, isDryRun, 15, prog));

            if (result.Success)
            {
                lblFlashStatus.ForeColor = Color.FromArgb(34, 197, 94);
                lblFlashStatus.Text      = isDryRun ? "✓ Dry Run concluído com sucesso!" : "✓ Gravação concluída!";
                MessageBox.Show(result.Message, isDryRun ? "Sucesso no Dry Run" : "Sucesso!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                lblFlashStatus.ForeColor = Color.FromArgb(239, 68, 68);
                lblFlashStatus.Text      = "Falha na operação.";
                MessageBox.Show(result.Message, "Falha na Operação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            lblFlashStatus.ForeColor = Color.FromArgb(239, 68, 68);
            lblFlashStatus.Text      = "Erro inesperado.";
            MessageBox.Show($"Ocorreu um erro:\n{ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnFlashCooler.Enabled    = true;
            btnSaveBin.Enabled        = true;
            btnSaveBmp.Enabled        = true;
            btnLoad.Enabled           = true;
            btnDumpFlash.Enabled      = true;
            btnDetectCooler.Enabled   = true;
            btnCloseGigabyte.Enabled  = true;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // FLASH DUMP (Tarefa 9)
    // ═════════════════════════════════════════════════════════════════════════════

    private async void BtnDumpFlash_Click(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog
        {
            Filter   = "Arquivo de Imagem da Flash (*.bin)|*.bin",
            FileName = $"aorus_flash_dump_{DateTime.Now:yyyyMMdd_HHmmss}.bin",
            Title    = "Salvar Dump Completo da Flash SPI (Somente Leitura)"
        };

        if (sfd.ShowDialog() != DialogResult.OK) return;

        btnDumpFlash.Enabled   = false;
        btnFlashCooler.Enabled = false;
        prgFlash.Visible       = true;
        prgFlash.Value         = 5;
        lblFlashStatus.Visible = true;
        lblFlashStatus.Text    = "Iniciando leitura da memória Flash...";
        lblFlashStatus.ForeColor = AorusOrange;

        var prog = new Progress<(int Percent, string Status)>(report =>
        {
            prgFlash.Value = Math.Clamp(report.Percent, 0, 100);
            lblFlashStatus.Text = report.Status;
        });

        try
        {
            var result = await Task.Run(() => AorusScsiTransport.DumpFlashToFileAsync(sfd.FileName, prog));
            if (result.Success)
            {
                lblFlashStatus.ForeColor = Color.FromArgb(34, 197, 94);
                lblFlashStatus.Text      = "✓ Dump concluído!";
                MessageBox.Show(result.Message, "Dump da Flash Concluído", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                lblFlashStatus.ForeColor = Color.FromArgb(239, 68, 68);
                lblFlashStatus.Text      = "Falha no dump.";
                MessageBox.Show(result.Message, "Falha no Dump", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro durante a leitura da Flash: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnDumpFlash.Enabled   = true;
            btnFlashCooler.Enabled = true;
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // PROCESSOS CONCORRENTES (Tarefa 5)
    // ═════════════════════════════════════════════════════════════════════════════

    private void BtnCloseGigabyte_Click(object? sender, EventArgs e)
    {
        var running = AorusDiagnostics.GetRunningGigabyteProcesses();
        if (running.Count == 0)
        {
            MessageBox.Show("Nenhum processo Gigabyte conflitante (RGBFusion, AorusEngine, AIO_Service) está em execução no momento.", "Tudo Limpo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string procs = string.Join("\n", running.ConvertAll(p => $"• {p.Name} (PID: {p.Pid})"));
        var dr = MessageBox.Show(
            $"Foram detectados os seguintes processos da Gigabyte em execução:\n\n{procs}\n\n" +
            "Esses processos podem prender a conexão USB da bomba durante a troca de modo.\n\n" +
            "Deseja encerrar esses processos agora?",
            "Confirmar Encerramento de Processos Concorrentes",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (dr == DialogResult.Yes)
        {
            var res = AorusDiagnostics.CloseGigabyteProcesses();
            MessageBox.Show(res.Message, "Processos Encerrados", MessageBoxButtons.OK, MessageBoxIcon.Information);
            BtnDetectCooler_Click(sender, e);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // DEVICE DETECTION & DIAGNOSTIC
    // ═════════════════════════════════════════════════════════════════════════════

    private void BtnDetectCooler_Click(object? sender, EventArgs e)
    {
        lblDeviceStatus.Text      = "Analisando hardware USB...";
        lblDeviceStatus.ForeColor = AorusGray;

        AorusLogger.Log("DIAG", "--- Executando Diagnóstico de Hardware e USB ---");

        // 1. Processos concorrentes
        var procs = AorusDiagnostics.GetRunningGigabyteProcesses();
        if (procs.Count > 0)
        {
            AorusLogger.Log("WARN", $"Processos Gigabyte em execução: {string.Join(", ", procs.ConvertAll(p => $"{p.Name} (PID:{p.Pid})"))}");
        }
        else
        {
            AorusLogger.Log("DIAG", "Nenhum processo concorrente Gigabyte em execução.");
        }

        // 2. Estado PnP
        var pnp = AorusDiagnostics.DetectHoltekUsbState();
        AorusLogger.Log("DIAG", $"Classificação PnP: {pnp}");
        AorusLogger.Log("DIAG", $"Driver PnP: {pnp.ErrorDescription}");

        // 3. Disco físico SCSI
        var dev = AorusScsiTransport.DetectDevice();
        if (dev.IsFound)
        {
            lblDeviceStatus.ForeColor = Color.FromArgb(34, 197, 94);
            lblDeviceStatus.Text      = $"Conectado: {dev.ModelName}\nModo: {(pnp.Mode == HoltekOperatingMode.JaEmIap_PIDA126 ? "IAP (Bootloader)" : "Normal")}";
            AorusLogger.Log("DIAG", $"Disco físico encontrado: {dev.ModelName} em {dev.DevicePath}");
        }
        else
        {
            lblDeviceStatus.ForeColor = Color.FromArgb(239, 68, 68);
            lblDeviceStatus.Text      = $"Dispositivo físico não encontrado.\nEstado PnP: {pnp.Mode}";
            AorusLogger.Log("WARN", "Disco físico 'LCD_PLATFORM' não localizado no Win32_DiskDrive.");
        }
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
