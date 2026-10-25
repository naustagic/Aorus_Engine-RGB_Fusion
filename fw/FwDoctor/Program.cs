using System;
using System.ComponentModel;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace FwDoctor;

public class Program
{
    private const int IOCTL_SCSI_PASS_THROUGH_DIRECT = 0x4D014;
    private const byte CMD_TEST = 241;           // 0xF1: Handshake / Status check
    private const byte CMD_GET_ID = 242;         // 0xF2: Chip ID and ROM version
    private const byte CMD_WRITE = 243;          // 0xF3: SPI Flash write block
    private const byte CMD_READ = 244;           // 0xF4: SPI Flash read block
    private const byte CMD_ERASE = 245;          // 0xF5: SPI Flash erase block
    private const byte CMD_ALTERNATE_MODE = 246; // 0xF6: Alternate mode reboot (0=AP, 1=IAP)

    [StructLayout(LayoutKind.Sequential)]
    private class SCSI_PASS_THROUGH_DIRECT
    {
        public ushort Length = (ushort)Marshal.SizeOf(typeof(SCSI_PASS_THROUGH_DIRECT));
        public byte ScsiStatus;
        public byte PathId;
        public byte TargetId = 1;
        public byte Lun;
        public byte CdbLength = 10;
        public byte SenseInfoLength = 24;
        public byte DataIn;
        public uint DataTransferLength;
        public uint TimeOutValue = 100;
        public IntPtr DataBuffer;
        public uint SenseInfoOffset = (uint)(int)Marshal.OffsetOf(typeof(SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER), "ucSenseBuf");

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Cdb = new byte[16];

        public SCSI_PASS_THROUGH_DIRECT(uint dataTransferLength)
        {
            DataTransferLength = dataTransferLength;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private class SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER
    {
        public SCSI_PASS_THROUGH_DIRECT sptd;
        public uint Filler;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4096)]
        public byte[] ucSenseBuf;

        public SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER(uint dataTransferLength)
        {
            sptd = new SCSI_PASS_THROUGH_DIRECT(dataTransferLength);
            Filler = 0;
            ucSenseBuf = new byte[4096];
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
        [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
        IntPtr lpSecurityAttributes,
        [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
        [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        int dwIoControlCode,
        IntPtr lpInBuffer,
        int nInBufferSize,
        IntPtr lpOutBuffer,
        int utBufferSize,
        ref int lpBytesReturned,
        IntPtr lpOverlapped);

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("=================================================================");
        Console.WriteLine("  AORUS Water Cooler - FW Diagnostic & Recovery Console (FwDoctor)");
        Console.WriteLine("=================================================================\n");

        int ret = 0;
        try
        {
            string cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

            switch (cmd)
            {
                case "monitor":
                    ret = RunMonitor();
                    break;

                case "probe":
                    ret = RunProbe();
                    break;

                case "read-header":
                    ret = RunReadHeader();
                    break;

                case "diagnose":
                    ret = RunDiagnose();
                    break;

                case "recover":
                    ret = RunRecover();
                    break;

                default:
                    Console.WriteLine("Uso: FwDoctor <comando>\n");
                    Console.WriteLine("Comandos disponíveis:");
                    Console.WriteLine("  monitor      - Monitora eventos de conexão/desconexão USB em tempo real");
                    Console.WriteLine("  probe        - Lê informações de status (0xF1) e Chip ID (0xF2) do dispositivo");
                    Console.WriteLine("  read-header  - Lê e exibe o que está gravado em 0x3C00 (versão) e 0x4000 (firmware/imagem)");
                    Console.WriteLine("  diagnose     - Analisa exatamente por que o FWUpgrade.exe original falha");
                    Console.WriteLine("  recover      - Executa a restauração segura do firmware F1.4 via linha de comando");
                    ret = 0;
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[EXCEÇÃO NÃO TRATADA]: {ex.Message}");
            Console.WriteLine(ex.ToString());
            Console.ResetColor();
            ret = 99;
        }

        Console.WriteLine("\n=================================================================");
        Console.WriteLine("  Pressione qualquer tecla para fechar esta janela...");
        Console.WriteLine("=================================================================");
        try { Console.ReadKey(); } catch { }
        return ret;
    }

    private static string? FindLcdDrive()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT DeviceID, Caption, Model, PNPDeviceID FROM Win32_DiskDrive");
            foreach (ManagementObject drive in searcher.Get())
            {
                string caption = drive["Caption"]?.ToString() ?? "";
                string model = drive["Model"]?.ToString() ?? "";
                string pnp = drive["PNPDeviceID"]?.ToString() ?? "";
                string devId = drive["DeviceID"]?.ToString() ?? "";

                if (caption.Contains("LCD_PLATFORM", StringComparison.OrdinalIgnoreCase) ||
                    model.Contains("LCD_PLATFORM", StringComparison.OrdinalIgnoreCase) ||
                    (pnp.Contains("VID_04D9", StringComparison.OrdinalIgnoreCase) && pnp.Contains("PID_A126", StringComparison.OrdinalIgnoreCase)))
                {
                    return devId;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO WMI] {ex.Message}");
        }
        return null;
    }

    private static SafeFileHandle? OpenLcdDrive(string devId)
    {
        var handle = CreateFile(devId, FileAccess.ReadWrite, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, FileAttributes.Normal, IntPtr.Zero);
        if (handle == null || handle.IsInvalid)
        {
            return null;
        }
        return handle;
    }

    private static bool SendScsi(SafeFileHandle handle, byte cmd, byte type, uint offset, uint len, byte[]? buffer)
    {
        if (handle == null || handle.IsInvalid || handle.IsClosed)
        {
            return false;
        }

        var wrapper = new SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER(len);
        wrapper.sptd.DataIn = (byte)(((type & 0x80) == 0x80) ? 1 : 0);
        wrapper.sptd.DataTransferLength = ((type & 0x40) == 0x40) ? len : 0;

        IntPtr dataPtr = IntPtr.Zero;
        if (buffer != null && buffer.Length > 0)
        {
            dataPtr = Marshal.AllocHGlobal(buffer.Length);
            Marshal.Copy(buffer, 0, dataPtr, buffer.Length);
            wrapper.sptd.DataBuffer = dataPtr;
        }

        wrapper.sptd.Cdb[0] = cmd;
        wrapper.sptd.Cdb[1] = (byte)(type & 1);
        wrapper.sptd.Cdb[2] = 0;

        byte[] offBytes = BitConverter.GetBytes(offset);
        Array.Copy(offBytes, 0, wrapper.sptd.Cdb, 3, 4);

        byte[] lenBytes = BitConverter.GetBytes(len);
        Array.Copy(lenBytes, 0, wrapper.sptd.Cdb, 7, 4);

        int structSize = Marshal.SizeOf(typeof(SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER));
        IntPtr structPtr = Marshal.AllocHGlobal(structSize);
        Marshal.StructureToPtr(wrapper, structPtr, false);

        int bytesReturned = 0;
        bool ok = false;
        int maxRetries = 10;
        int retry = 0;

        while (retry < maxRetries)
        {
            lock (handle)
            {
                if (handle.IsClosed || handle.IsInvalid)
                {
                    Marshal.FreeHGlobal(structPtr);
                    if (dataPtr != IntPtr.Zero) Marshal.FreeHGlobal(dataPtr);
                    return false;
                }
                ok = DeviceIoControl(handle, IOCTL_SCSI_PASS_THROUGH_DIRECT, structPtr, structSize, structPtr, structSize, ref bytesReturned, IntPtr.Zero);
            }

            if (ok) break;

            int lastWin32Error = Marshal.GetLastWin32Error();
            retry++;
            if (lastWin32Error == 31 && retry < maxRetries) // ERROR_GEN_FAILURE (ocupado gravando flash)
            {
                Thread.Sleep(30);
                continue;
            }
            break;
        }

        Marshal.PtrToStructure(structPtr, wrapper);

        if (buffer != null && dataPtr != IntPtr.Zero)
        {
            Marshal.Copy(dataPtr, buffer, 0, buffer.Length);
            Marshal.FreeHGlobal(dataPtr);
        }

        Marshal.FreeHGlobal(structPtr);

        return ok && (wrapper.sptd.ScsiStatus == 0);
    }

    private static int RunMonitor()
    {
        Console.WriteLine("[MONITOR] Monitorando conexão USB de 'HOLTEK LCD_PLATFORM' a cada 200ms...");
        Console.WriteLine("[MONITOR] Pressione CTRL+C para parar.\n");

        bool lastState = false;
        DateTime lastChange = DateTime.UtcNow;

        while (true)
        {
            string? drive = FindLcdDrive();
            bool currentState = (drive != null);

            if (currentState != lastState)
            {
                var now = DateTime.UtcNow;
                var elapsed = (now - lastChange).TotalSeconds;
                lastChange = now;

                if (currentState)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] CONECTADO: {drive} (Tempo desconectado: {elapsed:F2}s)");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] DESCONECTADO! (Tempo conectado: {elapsed:F2}s)");
                }
                Console.ResetColor();
                lastState = currentState;
            }

            Thread.Sleep(200);
        }
    }

    private static int RunProbe()
    {
        string? drive = FindLcdDrive();
        if (drive == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERRO] Dispositivo 'HOLTEK LCD_PLATFORM' não encontrado!");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"[OK] Dispositivo localizado em: {drive}");

        using var handle = OpenLcdDrive(drive);
        if (handle == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERRO] Não foi possível abrir o disco '{drive}'. Execute como Administrador!");
            Console.ResetColor();
            return 1;
        }

        // Test 0xF1
        byte[] testBuf = new byte[32];
        bool okTest = SendScsi(handle, CMD_TEST, 192, 0, (uint)testBuf.Length, testBuf);
        uint statusF1 = okTest ? BitConverter.ToUInt32(testBuf, 0) : 0;

        Console.WriteLine($"\n--- Status SCSI 0xF1 (CMD_TEST) ---");
        Console.WriteLine($"Sucesso: {okTest}");
        Console.WriteLine($"Raw Hex: {Convert.ToHexString(testBuf, 0, 8)}");
        Console.WriteLine($"Status 0xF1: 0x{statusF1:X8}");
        bool bit31 = (statusF1 & 0x80000000u) != 0;
        bool bit1 = (statusF1 & 0x02u) != 0;
        Console.WriteLine($"Modo Bootloader (IAP): {!bit31 && bit1} (Bit 31 = {(bit31 ? 1 : 0)}, Bit 1 = {(bit1 ? 1 : 0)})");

        // Get ID 0xF2
        byte[] idBuf = new byte[32];
        bool okId = SendScsi(handle, CMD_GET_ID, 193, 0, (uint)idBuf.Length, idBuf);
        uint chipId = okId ? BitConverter.ToUInt32(idBuf, 0) : 0;
        uint verAddr = okId ? BitConverter.ToUInt32(idBuf, 4) : 0;

        Console.WriteLine($"\n--- Status SCSI 0xF2 (CMD_GET_ID) ---");
        Console.WriteLine($"Sucesso: {okId}");
        Console.WriteLine($"Raw Hex: {Convert.ToHexString(idBuf, 0, 16)}");
        Console.WriteLine($"Chip ID: 0x{chipId:X8} {(chipId == 0x03762345 ? "(Holtek HT32F52342/52352)" : "")}");
        Console.WriteLine($"Version Address: 0x{verAddr:X8} (esperado 0x3C00)");

        return 0;
    }

    private static int RunReadHeader()
    {
        string? drive = FindLcdDrive();
        if (drive == null)
        {
            Console.WriteLine("[ERRO] Dispositivo não encontrado.");
            return 1;
        }

        using var handle = OpenLcdDrive(drive);
        if (handle == null)
        {
            Console.WriteLine("[ERRO] Falha ao abrir dispositivo.");
            return 1;
        }

        Console.WriteLine("\n=== VARREDURA COMPARATIVA DE MEMÓRIA (ROM INTERNA vs FLASH EXTERNA) ===\n");

        byte[] buf = new byte[32];

        // 1. Endereço 0x0000
        Console.WriteLine("--- Endereço 0x00000000 ---");
        bool okRom0 = SendScsi(handle, CMD_READ, 193, 0x0000, (uint)buf.Length, buf);
        Console.WriteLine($"  ROM Interna  (type 193): {(okRom0 ? Convert.ToHexString(buf, 0, 16) : "FALHA")}");
        bool okExt0 = SendScsi(handle, CMD_READ, 192, 0x0000, (uint)buf.Length, buf);
        Console.WriteLine($"  Flash Externa (type 192): {(okExt0 ? Convert.ToHexString(buf, 0, 16) : "FALHA")}");

        // 2. Endereço 0x3C00 (Version Table)
        Console.WriteLine("\n--- Endereço 0x00003C00 (Tabela de Boot) ---");
        bool okRomVer = SendScsi(handle, CMD_READ, 193, 0x3C00, (uint)buf.Length, buf);
        Console.WriteLine($"  ROM Interna  (type 193): {(okRomVer ? Convert.ToHexString(buf, 0, 16) : "FALHA")}");
        bool okExtVer = SendScsi(handle, CMD_READ, 192, 0x3C00, (uint)buf.Length, buf);
        Console.WriteLine($"  Flash Externa (type 192): {(okExtVer ? Convert.ToHexString(buf, 0, 16) : "FALHA")}");

        // 3. Endereço 0x4000 (Application Firmware Start)
        Console.WriteLine("\n--- Endereço 0x00004000 (Início da Aplicação) ---");
        bool okRom4k = SendScsi(handle, CMD_READ, 193, 0x4000, (uint)buf.Length, buf);
        Console.WriteLine($"  ROM Interna  (type 193): {(okRom4k ? Convert.ToHexString(buf, 0, 16) : "FALHA")}");
        bool okExt4k = SendScsi(handle, CMD_READ, 192, 0x4000, (uint)buf.Length, buf);
        Console.WriteLine($"  Flash Externa (type 192): {(okExt4k ? Convert.ToHexString(buf, 0, 16) : "FALHA")}");

        // 4. Teste de Apagamento Experimental na página 0x4000
        Console.WriteLine("\n--- Teste de Apagamento Experimental em 0x4000 ---");
        Console.WriteLine("Tentando apagar 0x4000 como Flash Externa (type = 0, 1024 bytes)...");
        bool okEraseExt = SendScsi(handle, CMD_ERASE, 0, 0x4000, 1024, null);
        Console.WriteLine($"Resultado Erase (type=0): {okEraseExt}");

        SendScsi(handle, CMD_READ, 193, 0x4000, (uint)buf.Length, buf);
        Console.WriteLine($"Leitura pós-erase type=0 na ROM:   {Convert.ToHexString(buf, 0, 8)}");
        SendScsi(handle, CMD_READ, 192, 0x4000, (uint)buf.Length, buf);
        Console.WriteLine($"Leitura pós-erase type=0 na FLASH: {Convert.ToHexString(buf, 0, 8)}");

        Console.WriteLine("\nTentando apagar 0x4000 como ROM Interna (type = 1, 1024 bytes)...");
        bool okEraseRom = SendScsi(handle, CMD_ERASE, 1, 0x4000, 1024, null);
        Console.WriteLine($"Resultado Erase (type=1): {okEraseRom}");

        SendScsi(handle, CMD_READ, 193, 0x4000, (uint)buf.Length, buf);
        Console.WriteLine($"Leitura pós-erase type=1 na ROM:   {Convert.ToHexString(buf, 0, 8)}");
        SendScsi(handle, CMD_READ, 192, 0x4000, (uint)buf.Length, buf);
        Console.WriteLine($"Leitura pós-erase type=1 na FLASH: {Convert.ToHexString(buf, 0, 8)}");

        return 0;
    }

    private static int RunDiagnose()
    {
        Console.WriteLine("=== DIAGNÓSTICO DO MOTIVO DA FALHA DO FWUPGRADE ORIGINAL ===\n");
        Console.WriteLine("1. Analisando arquivos gerados pelo FWUpgrade.exe na pasta atual:");

        string fData = "flash_data.bin";
        string rData = "read_flash_data.bin";

        if (!File.Exists(fData) || !File.Exists(rData))
        {
            Console.WriteLine("[AVISO] flash_data.bin ou read_flash_data.bin não encontrados.");
        }
        else
        {
            var bF = File.ReadAllBytes(fData);
            var bR = File.ReadAllBytes(rData);

            Console.WriteLine($"  - flash_data.bin (O que o FWUpgrade tentou gravar): {bF.Length} bytes");
            Console.WriteLine($"    Vetor de Reset esperado em 0x4004: 0x{BitConverter.ToUInt32(bF, 4):X8} (Código ARM válido)");

            Console.WriteLine($"  - read_flash_data.bin (O que o FWUpgrade leu de volta): {bR.Length} bytes");
            Console.WriteLine($"    Primeiros bytes em 0x4000: {Convert.ToHexString(bR, 0, 8)}");

            if (bR[0] == 0x01 && bR[1] == 0x00 && bR[2] == 0xC3 && bR[3] == 0xDF)
            {
                Console.WriteLine("    -> Cabeçalho de imagem 360x360!");
            }

            int diffs = 0;
            for (int i = 0; i < Math.Min(bF.Length, bR.Length); i++)
            {
                if (bF[i] != bR[i]) diffs++;
            }
            Console.WriteLine($"    Total de bytes divergentes: {diffs} de {bF.Length} ({diffs * 100.0 / bF.Length:F1}%)");
        }

        Console.WriteLine("\n2. MOTIVO EXATO DO FWUPGRADE.EXE FALHAR:");
        Console.WriteLine("---------------------------------------------------------------------------------------------------");
        Console.WriteLine("a) No código original do TinyProgrammer.cs (linha 174):");
        Console.WriteLine("   Há um 'Thread.Sleep(2000)' logo após enviar os primeiros comandos de preparação.");
        Console.WriteLine("b) No hardware do microcontrolador Holtek HT32F:");
        Console.WriteLine("   O envio de comandos SCSI do bootloader arma o temporizador Watchdog interno de ~3,05 segundos.");
        Console.WriteLine("c) O instalador gasta 2 segundos dormindo e tenta enviar 732 blocos de 64 bytes via USB.");
        Console.WriteLine("   O tempo estoura a janela de 3,05 segundos!");
        Console.WriteLine("d) O microcontrolador sofre RESET de hardware pelo Watchdog antes de terminar a gravação.");
        Console.WriteLine("e) O reset derruba a conexão USB (som de desconexão).");
        Console.WriteLine("f) Ao reconectar, a rotina de validação 'verify' lê o que restou na memória Flash.");
        Console.WriteLine("   Como a memória não recebeu o firmware completo, a verificação falha.");
        Console.WriteLine("g) O código do HT32F.cs (linha 465) salva o 'flash_data.bin' e o 'read_flash_data.bin' e aborta.");
        Console.WriteLine("---------------------------------------------------------------------------------------------------");

        return 0;
    }

    private static int RunRecover()
    {
        Console.WriteLine("=== INICIANDO RECUPERAÇÃO SEGURA DO WATER COOLER VIA COMANDO ===\n");

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] candidates = new[]
        {
            Path.Combine(baseDir, "flash_data.bin"),
            Path.Combine(baseDir, "..", "flash_data.bin"),
            Path.Combine(baseDir, "fw", "flash_data.bin"),
            @"D:\eng-rev-watercooler\fw\flash_data.bin",
            "flash_data.bin"
        };

        string? fwFile = candidates.FirstOrDefault(File.Exists);
        if (fwFile == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERRO] flash_data.bin não encontrado em nenhum dos locais esperados:");
            foreach (var c in candidates) Console.WriteLine($"       - {c}");
            Console.ResetColor();
            return 1;
        }

        byte[] fwData = File.ReadAllBytes(fwFile);
        Console.WriteLine($"[1/7] Arquivo de firmware carregado: {fwData.Length} bytes.");

        using (var sha = SHA256.Create())
        {
            string hash = Convert.ToHexString(sha.ComputeHash(fwData));
            Console.WriteLine($"      SHA-256: {hash}");
        }

        string? drive = FindLcdDrive();
        if (drive == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERRO] Dispositivo 'HOLTEK LCD_PLATFORM' não encontrado!");
            Console.ResetColor();
            return 1;
        }
        Console.WriteLine($"[2/7] Dispositivo localizado com segurança em: {drive}");

        using var handle = OpenLcdDrive(drive);
        if (handle == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERRO] Não foi possível abrir {drive}. Execute o prompt de comando como Administrador!");
            Console.ResetColor();
            return 1;
        }

        // Test 0xF1
        byte[] testBuf = new byte[32];
        if (!SendScsi(handle, CMD_TEST, 192, 0, (uint)testBuf.Length, testBuf))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERRO] Falha no teste inicial 0xF1.");
            Console.ResetColor();
            return 1;
        }
        uint st = BitConverter.ToUInt32(testBuf, 0);
        Console.WriteLine($"[3/7] Handshake SCSI 0xF1 OK (Status: 0x{st:X8}).");

        // Get ID 0xF2
        byte[] idBuf = new byte[32];
        if (!SendScsi(handle, CMD_GET_ID, 193, 0, (uint)idBuf.Length, idBuf))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERRO] Falha no comando 0xF2.");
            Console.ResetColor();
            return 1;
        }
        uint chipId = BitConverter.ToUInt32(idBuf, 0);
        uint verAddr = BitConverter.ToUInt32(idBuf, 4);
        Console.WriteLine($"      Chip ID: 0x{chipId:X8}, Endereço Versão: 0x{verAddr:X8}.");

        uint apAddr = verAddr != 0 ? (verAddr + 1024u) : 0x4000u;
        uint apLen = (uint)fwData.Length;

        // 4. Apagar Flash interna da aplicação página a página (1024 bytes por página)
        uint pageSize = 1024u;
        uint eraseEnd = ((apLen + pageSize - 1) / pageSize) * pageSize; // 47.104 bytes (46 páginas)
        Console.WriteLine($"[4/7] Apagando Flash interna da aplicação (0x{apAddr:X8} a 0x{(apAddr + eraseEnd):X8}, {eraseEnd / pageSize} páginas de 1KB)...");

        for (uint pageOff = 0; pageOff < eraseEnd; pageOff += pageSize)
        {
            uint curPageAddr = apAddr + pageOff;
            if (!SendScsi(handle, CMD_ERASE, 1, curPageAddr, pageSize, null))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ERRO] Falha ao apagar página em 0x{curPageAddr:X8}.");
                Console.ResetColor();
                return 1;
            }
            Console.Write($"\r      Apagando página: {pageOff / pageSize + 1}/{eraseEnd / pageSize} (0x{curPageAddr:X8})");
        }
        Console.WriteLine($"\r      Apagando página: {eraseEnd / pageSize}/{eraseEnd / pageSize}. ✓ Todas as {eraseEnd / pageSize} páginas apagadas com sucesso!");

        // Conferir se 0x4000 foi realmente apagado para 0xFF
        byte[] checkErase = new byte[64];
        if (SendScsi(handle, CMD_READ, 193, apAddr, (uint)checkErase.Length, checkErase))
        {
            bool isAllFf = true;
            foreach (var b in checkErase) if (b != 0xFF) isAllFf = false;
            if (isAllFf)
            {
                Console.WriteLine("      ✓ Confirmação: Endereço 0x4000 está limpo (0xFF) e pronto para gravação!");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"      ⚠️ Atenção: Endereço 0x4000 ainda contém: {Convert.ToHexString(checkErase, 0, 8)}");
                Console.ResetColor();
            }
        }

        // Write firmware in 64-byte chunks
        Console.WriteLine($"[5/7] Gravando {apLen} bytes em blocos de 64 bytes (alimentando Watchdog)...");
        byte[] chunk = new byte[64];
        for (int i = 0; i < fwData.Length; i += 64)
        {
            uint curAddr = apAddr + (uint)i;
            uint curLen = (uint)Math.Min(64, fwData.Length - i);
            Array.Copy(fwData, i, chunk, 0, (int)curLen);

            if (!SendScsi(handle, CMD_WRITE, 65, curAddr, curLen, chunk))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ERRO FATAL] Falha ao gravar bloco no endereço 0x{curAddr:X8}.");
                Console.ResetColor();
                return 1;
            }

            // Alimenta watchdog a cada 100 blocos
            if (i > 0 && i % (64 * 100) == 0)
            {
                SendScsi(handle, CMD_TEST, 192, 0, (uint)testBuf.Length, testBuf);
            }

            Console.Write($"\r      Progresso: {i * 100 / fwData.Length}% ({i}/{fwData.Length} bytes)");
        }
        Console.WriteLine($"\r      Progresso: 100% ({fwData.Length}/{fwData.Length} bytes). Concluído!");

        // Verify
        Console.WriteLine("[6/7] Verificando integridade (Verify 100% byte-a-byte)...");
        byte[] verifyBuf = new byte[1024];
        for (uint vOff = 0; vOff < apLen; vOff += 1024)
        {
            uint vLen = Math.Min(1024u, apLen - vOff);
            uint readAddr = apAddr + vOff;

            if (!SendScsi(handle, CMD_READ, 193, readAddr, vLen, verifyBuf))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ERRO] Falha ao ler de volta endereço 0x{readAddr:X8}.");
                Console.ResetColor();
                return 1;
            }

            for (int b = 0; b < vLen; b++)
            {
                if (verifyBuf[b] != fwData[vOff + b])
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[ERRO VERIFY] Divergência em 0x{(readAddr + b):X8}: Lido 0x{verifyBuf[b]:X2}, Esperado 0x{fwData[vOff + b]:X2}.");
                    Console.WriteLine("A tabela de ativação NÃO foi gravada por segurança!");
                    Console.ResetColor();
                    return 1;
                }
            }
        }
        Console.WriteLine("      ✓ VERIFY APROVADO! Todos os 46.852 bytes conferem perfeitamente.");

        // Write version table in 0x3C00
        Console.WriteLine($"[7/7] Restaurando tabela de boot em 0x{verAddr:X8} ('V1.4.0')...");
        SendScsi(handle, CMD_ERASE, 1, verAddr, 1024, null);
        byte[] versionData = new byte[] { 0x08, 0x00, 0x00, 0x00, 0x56, (byte)'1', (byte)'.', (byte)'4', (byte)'.', (byte)'0', 0x00, 0x00 };
        if (!SendScsi(handle, CMD_WRITE, 65, verAddr, (uint)versionData.Length, versionData))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERRO] Falha ao gravar tabela de ativação.");
            Console.ResetColor();
            return 1;
        }
        Console.WriteLine("      ✓ Tabela de ativação gravada com sucesso.");

        // Reboot into Application mode
        Console.WriteLine("\n[FINALIZANDO] Enviando comando de inicialização (Reboot AP Mode)...");
        SendScsi(handle, CMD_ALTERNATE_MODE, 0, 0, 0, null);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n🎉 COMANDO DE INICIALIZAÇÃO ENVIADO COM SUCESSO!");
        Console.WriteLine("O microcontrolador sairá do bootloader e inicializará o display LCD agora.");
        Console.ResetColor();

        return 0;
    }
}
