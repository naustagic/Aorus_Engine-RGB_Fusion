using System;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace AorusCoolerConverter.Core;

public class AorusDeviceInfo
{
    public string DevicePath { get; set; } = "";
    public string ModelName { get; set; } = "";
    public uint VersionAddress { get; set; }
    public bool IsIapMode { get; set; }
    public bool IsFound => !string.IsNullOrEmpty(DevicePath);
}

public class AorusScsiTransport : IDisposable
{
    private const int IOCTL_SCSI_PASS_THROUGH_DIRECT = 0x4D014;
    private const byte CMD_TEST = 241;           // 0xF1: Handshake / Status check
    private const byte CMD_GET_ID = 242;         // 0xF2: Chip ID and ROM version
    private const byte CMD_WRITE = 243;          // 0xF3: SPI Flash write block
    private const byte CMD_READ = 244;           // 0xF4: SPI Flash read block
    private const byte CMD_ERASE = 245;          // 0xF5: SPI Flash erase block
    private const byte CMD_ALTERNATE_MODE = 246; // 0xF6: Alternate mode reboot (0=AP, 1=IAP)

    public const uint BlockSize = 65536; // 64 KB SPI Flash Block Size

    private SafeFileHandle? _deviceHandle;
    private bool _disposed;

    public int LastWin32Error { get; private set; }

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

    /// <summary>
    /// Searches for the Aorus Cooler LCD_PLATFORM USB disk drive.
    /// Uses fast direct drive probe first, falling back to WMI if needed.
    /// </summary>
    public static AorusDeviceInfo DetectDevice()
    {
        var info = new AorusDeviceInfo();

        // 1. Fast direct probe of PhysicalDrive0 .. PhysicalDrive16
        for (int i = 0; i <= 16; i++)
        {
            string path = $@"\\.\PHYSICALDRIVE{i}";
            using var handle = CreateFile(
                path,
                FileAccess.ReadWrite,
                FileShare.ReadWrite,
                IntPtr.Zero,
                FileMode.Open,
                FileAttributes.Normal,
                IntPtr.Zero);

            if (handle != null && !handle.IsInvalid)
            {
                using var testTransport = new AorusScsiTransport();
                testTransport._deviceHandle = handle;

                uint status = testTransport.TestDevice();
                if (status != 0)
                {
                    var (ok, chipId, verAddr) = testTransport.GetChipId();
                    if (ok && (chipId & 0xFFFF0000u) == 0x03760000u)
                    {
                        info.DevicePath = path;
                        info.ModelName = $"HOLTEK LCD_PLATFORM (PhysicalDrive{i})";
                        info.VersionAddress = verAddr;
                        info.IsIapMode = (status & 0x80000000u) == 0x80000000u;
                        testTransport._deviceHandle = null; // Do not dispose handle, will be closed by caller
                        return info;
                    }
                }
                testTransport._deviceHandle = null;
            }
        }

        // 2. Fallback to WMI
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, Caption, Model, PNPDeviceID FROM Win32_DiskDrive WHERE Caption LIKE '%LCD_PLATFORM%' OR Model LIKE '%LCD_PLATFORM%' OR PNPDeviceID LIKE '%VID_04D9%'");

            foreach (ManagementObject drive in searcher.Get())
            {
                string caption = drive["Caption"]?.ToString() ?? "";
                string devId = drive["DeviceID"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(devId))
                {
                    info.DevicePath = devId;
                    info.ModelName = caption;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            AorusLogger.Log("ERR", $"Exceção ao buscar Win32_DiskDrive: {ex.Message}");
        }

        return info;
    }

    public bool OpenDevice(string devicePath)
    {
        Close();

        _deviceHandle = CreateFile(
            devicePath,
            FileAccess.ReadWrite,
            FileShare.ReadWrite,
            IntPtr.Zero,
            FileMode.Open,
            FileAttributes.Normal,
            IntPtr.Zero);

        if (_deviceHandle == null || _deviceHandle.IsInvalid)
        {
            LastWin32Error = Marshal.GetLastWin32Error();
            AorusLogger.Log("ERR", $"Falha ao abrir handle '{devicePath}': Win32 Error {LastWin32Error}");
            return false;
        }

        LastWin32Error = 0;
        AorusLogger.Log("HANDLE", $"Porta '{devicePath}' aberta com sucesso. Handle válido.");
        return true;
    }

    public void Close()
    {
        if (_deviceHandle != null)
        {
            if (!_deviceHandle.IsClosed)
            {
                _deviceHandle.Dispose();
                AorusLogger.Log("HANDLE", "Handle USB/SCSI anterior fechado e descartado (Dispose).");
            }
            _deviceHandle = null;
        }
    }

    public bool SendScsiCommand(byte cmd, byte type, uint offset, uint len, byte[]? buffer)
    {
        if (_deviceHandle == null || _deviceHandle.IsInvalid || _deviceHandle.IsClosed)
        {
            AorusLogger.Log("ERR", $"Tentativa de envio de comando 0x{cmd:X2} com handle inválido ou fechado.");
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

        AorusLogger.LogHex("SCSI_TX", $"Opcode 0x{cmd:X2} CDB", wrapper.sptd.Cdb, 10);

        lock (_deviceHandle)
        {
            ok = DeviceIoControl(_deviceHandle, IOCTL_SCSI_PASS_THROUGH_DIRECT, structPtr, structSize, structPtr, structSize, ref bytesReturned, IntPtr.Zero);
        }

        Marshal.PtrToStructure(structPtr, wrapper);

        if (buffer != null && dataPtr != IntPtr.Zero)
        {
            Marshal.Copy(dataPtr, buffer, 0, buffer.Length);
            Marshal.FreeHGlobal(dataPtr);

            if (wrapper.sptd.DataIn == 1)
            {
                AorusLogger.LogHex("SCSI_RX", $"Resposta Opcode 0x{cmd:X2}", buffer, Math.Min(buffer.Length, 32));
            }
        }

        Marshal.FreeHGlobal(structPtr);

        bool success = ok && wrapper.sptd.ScsiStatus == 0;
        if (!success)
        {
            AorusLogger.Log("WARN", $"Comando SCSI 0x{cmd:X2} retornou ok={ok}, ScsiStatus={wrapper.sptd.ScsiStatus}");
        }

        return success;
    }

    /// <summary>
    /// Tests device status via SCSI CMD 241 (0xF1).
    /// </summary>
    public uint TestDevice()
    {
        byte[] array = new byte[32];
        if (SendScsiCommand(CMD_TEST, 192, 0, (uint)array.Length, array))
        {
            uint status = BitConverter.ToUInt32(array, 0);
            return status;
        }
        return 0;
    }

    /// <summary>
    /// Returns true if device is currently in IAP programming mode.
    /// Bit 31 (0x80000000) of testDevice status indicates IAP mode.
    /// </summary>
    public bool IsIapMode()
    {
        uint num = TestDevice();
        return num != 0 && (num & 0x80000000u) == 0x80000000u;
    }

    /// <summary>
    /// Reads Holtek Chip ID and Version Address via SCSI CMD 242 (0xF2).
    /// </summary>
    public (bool Success, uint ChipId, uint VersionAddress) GetChipId()
    {
        byte[] array = new byte[32];
        if (SendScsiCommand(CMD_GET_ID, 193, 0, (uint)array.Length, array))
        {
            uint chipId = BitConverter.ToUInt32(array, 0);
            uint verAddr = BitConverter.ToUInt32(array, 4);
            return (true, chipId, verAddr);
        }
        return (false, 0, 0);
    }

    /// <summary>
    /// Reads a block of Flash memory via SCSI CMD 244 (0xF4).
    /// </summary>
    public bool ReadFlashBlock(uint offset, uint length, byte[] buffer)
    {
        return SendScsiCommand(CMD_READ, 193, offset, length, buffer);
    }

    /// <summary>
    /// Reboots device to IAP mode (mode=1) or normal operating mode (mode=0) via CMD 246 (0xF6).
    /// </summary>
    public bool RebootDevice(byte mode)
    {
        AorusLogger.Log("REBOOT", $"Enviando comando 0xF6 (CMD_ALTERNATE_MODE) com mode={mode}...");
        return SendScsiCommand(CMD_ALTERNATE_MODE, 0, mode, mode, null);
    }

    /// <summary>
    /// Dumps the SPI Flash contents using CMD_READ (0xF4) and calculates the SHA-256 hash.
    /// Purely read-only; does not write or erase anything.
    /// </summary>
    public static async Task<(bool Success, string Message, string? Sha256)> DumpFlashToFileAsync(
        string outputPath,
        IProgress<(int Percent, string Status)>? progress = null,
        System.Threading.CancellationToken ct = default)
    {
        AorusLogger.Log("DUMP", $"Iniciando dump de leitura da Flash para: {outputPath}");

        var dev = DetectDevice();
        if (!dev.IsFound)
        {
            AorusLogger.Log("ERR", "Dispositivo não encontrado para efetuar dump.");
            return (false, "Water Cooler não encontrado na porta USB.", null);
        }

        using var transport = new AorusScsiTransport();
        if (!transport.OpenDevice(dev.DevicePath))
        {
            return (false, $"Não foi possível abrir o dispositivo (Erro Win32: {transport.LastWin32Error}).", null);
        }

        uint totalSize = 10485760; // 10 MB Flash
        uint chunkSize = 4096;     // 4 KB por pacote de leitura
        byte[] chunk = new byte[chunkSize];

        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var sha256 = SHA256.Create();

        for (uint offset = 0; offset < totalSize; offset += chunkSize)
        {
            ct.ThrowIfCancellationRequested();
            int pct = (int)((long)offset * 100 / totalSize);
            progress?.Report((pct, $"Lendo Flash SPI... {offset / 1024} KB / {totalSize / 1024} KB ({pct}%)"));

            if (!transport.ReadFlashBlock(offset, chunkSize, chunk))
            {
                AorusLogger.Log("ERR", $"Falha na leitura do bloco no offset 0x{offset:X8}. Abortando dump.");
                return (false, $"Falha na leitura do bloco no offset 0x{offset:X8}.", null);
            }

            fileStream.Write(chunk, 0, chunk.Length);
            sha256.TransformBlock(chunk, 0, chunk.Length, null, 0);
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        string hash = BitConverter.ToString(sha256.Hash!).Replace("-", "").ToLowerInvariant();

        progress?.Report((100, "✓ Dump concluído com sucesso!"));
        AorusLogger.Log("DUMP", $"Dump de 10 MB salvo com sucesso. SHA-256: {hash}");
        return (true, $"Dump de 10 MB salvo com sucesso!\nSHA-256: {hash}", hash);
    }

    /// <summary>
    /// Flashes payload or executes Dry Run diagnostic with strict verification and zero automatic retries.
    /// </summary>
    public static async Task<(bool Success, string Message)> FlashPayloadToCoolerAsync(
        byte[] rawBinData,
        bool isDryRun = true,
        int timeoutSeconds = 15,
        IProgress<(int Percent, string Status)>? progress = null,
        System.Threading.CancellationToken ct = default)
    {
        AorusLogger.Log("FLOW", $"=== Início do Fluxo (Modo Dry Run: {isDryRun}, Timeout: {timeoutSeconds}s) ===");

        // 1. Diagnóstico de processos concorrentes
        var procs = AorusDiagnostics.GetRunningGigabyteProcesses();
        if (procs.Count > 0)
        {
            string procList = string.Join(", ", procs.ConvertAll(p => $"{p.Name} (PID:{p.Pid})"));
            AorusLogger.Log("WARN", $"ATENÇÃO: Processos Gigabyte concorrentes em execução: {procList}");
            AorusLogger.Log("WARN", "Recomenda-se fechar esses processos antes de gravar para evitar conflitos no descritor USB.");
        }

        // 2. Detecção inicial de estado USB via Win32_PnPEntity (Tarefa 1)
        progress?.Report((5, "Analisando estado USB do hardware..."));
        var pnpStatus = AorusDiagnostics.DetectHoltekUsbState();
        AorusLogger.Log("DIAG", $"Classificação PnP: {pnpStatus}");
        AorusLogger.Log("DIAG", $"Driver / Código de Problema: {pnpStatus.ErrorDescription}");

        if (pnpStatus.ConfigManagerErrorCode != 0)
        {
            AorusLogger.Log("ERR", $"Dispositivo USB com problema reportado pelo Windows: {pnpStatus.ErrorDescription}");
            return (false, $"Dispositivo USB com falha no Windows: {pnpStatus.ErrorDescription}");
        }

        // 3. Verificar dispositivo físico
        var dev = DetectDevice();
        AorusLogger.Log("DIAG", dev.IsFound ? $"Dispositivo encontrado: {dev.ModelName} em {dev.DevicePath}" : "Nenhum disco físico Aorus detectado imediatamente.");

        using (var transport = new AorusScsiTransport())
        {
            // Se estiver em modo normal (PID_3020), enviar reinício 0xF6
            if (pnpStatus.Mode == HoltekOperatingMode.ModoNormal_PID3020)
            {
                if (!dev.IsFound || !transport.OpenDevice(dev.DevicePath))
                {
                    AorusLogger.Log("ERR", "Dispositivo em modo normal (PID_3020) não pôde ser aberto para envio de 0xF6.");
                    return (false, "Não foi possível abrir o dispositivo em modo normal para solicitar reinício IAP.");
                }

                progress?.Report((10, "Enviando comando de reinício para modo IAP (0xF6)..."));
                transport.RebootDevice(1);

                // Tarefa 2: Fechar e descartar explicitamente o handle anterior
                transport.Close();

                // Tarefa 3: Polling da enumeração USB a cada ~100 ms (timeout 15s)
                progress?.Report((15, "Aguardando reconexão em Modo IAP (polling a cada 100ms)..."));
                bool reconnected = false;
                int maxIterations = timeoutSeconds * 10;

                for (int i = 0; i < maxIterations; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(100, ct);

                    if (i % 10 == 0)
                    {
                        int sec = (i / 10) + 1;
                        progress?.Report((15 + (i * 15 / maxIterations), $"Aguardando reconexão IAP... ({sec}s)"));

                        // Verificar se cooler ignorou o reset e continuou em PID_3020
                        if (sec >= 3)
                        {
                            var currentPnp = AorusDiagnostics.DetectHoltekUsbState();
                            if (currentPnp.Mode == HoltekOperatingMode.ModoNormal_PID3020)
                            {
                                AorusLogger.Log("WARN", $"[{sec}s] Cooler ainda reporta PID_3020 (modo normal).");
                            }
                        }
                    }

                    var checkDev = DetectDevice();
                    if (checkDev.IsFound)
                    {
                        if (transport.OpenDevice(checkDev.DevicePath))
                        {
                            if (transport.IsIapMode())
                            {
                                reconnected = true;
                                dev = checkDev;
                                AorusLogger.Log("POLL", $"Dispositivo reconectado com sucesso em modo IAP em {checkDev.DevicePath} após {(i + 1) * 100}ms.");
                                break;
                            }
                            transport.Close();
                        }
                    }
                }

                if (!reconnected)
                {
                    AorusLogger.Log("ERR", $"Timeout de {timeoutSeconds}s expirado sem confirmação do modo IAP.");
                    return (false, $"O Water Cooler não respondeu em modo IAP dentro do tempo limite de {timeoutSeconds}s.");
                }
            }
            else if (pnpStatus.Mode == HoltekOperatingMode.JaEmIap_PIDA126 || dev.IsFound)
            {
                // Tarefa 1: JÁ EM IAP — NÃO enviar 0xF6, ir direto ao handshake!
                AorusLogger.Log("DIAG", "Cooler JÁ está em Modo IAP (PID_A126). Pulando comando de reinício 0xF6.");
                if (!dev.IsFound || !transport.OpenDevice(dev.DevicePath))
                {
                    AorusLogger.Log("ERR", $"Não foi possível abrir a porta de comunicação IAP em {dev.DevicePath} (Win32 Error: {transport.LastWin32Error}).");
                    return (false, $"Não foi possível abrir a porta de comunicação IAP (Erro Win32: {transport.LastWin32Error}).");
                }
            }
            else
            {
                AorusLogger.Log("ERR", "Nenhum Water Cooler Holtek detectado na porta USB.");
                return (false, "Water Cooler não detectado na porta USB. Verifique a conexão.");
            }

            // Tarefa 4: Handshake obrigatório (0xF1) antes de qualquer gravação
            progress?.Report((35, "Executando Handshake IAP (CMD 0xF1)..."));
            uint testStatus = transport.TestDevice();
            AorusLogger.Log("HANDSHAKE", $"Resultado do teste de handshake 0xF1: 0x{testStatus:X8}");

            if (testStatus == 0 || (testStatus & 0x80000000u) == 0)
            {
                AorusLogger.Log("ERR", $"Handshake FALHOU: Resposta 0x{testStatus:X8} NÃO contém o bit IAP (0x80000000). Abortando sem escrever nada!");
                transport.Close();
                return (false, $"Handshake IAP falhou. Resposta 0x{testStatus:X8} não confirmou o bootloader. Gravação cancelada por segurança.");
            }

            AorusLogger.Log("HANDSHAKE", "✓ Bit IAP confirmado com sucesso (0x80000000 presente).");

            // Obter Chip ID e Versão da ROM (0xF2)
            var (okChip, chipId, verAddr) = transport.GetChipId();
            if (okChip)
            {
                AorusLogger.Log("HANDSHAKE", $"Chip ID (0xF2): 0x{chipId:X8}, Versão: 0x{verAddr:X8}");
                if ((chipId & 0xFFFF0000u) == 0x03760000u)
                {
                    AorusLogger.Log("HANDSHAKE", "✓ Assinatura do microcontrolador Holtek HT32F validada com sucesso!");
                }
            }

            // Tarefa 7 / Restrição: Modo Dry Run (Somente Leitura e Validação)
            if (isDryRun)
            {
                progress?.Report((100, "✓ Dry Run concluído com sucesso!"));
                AorusLogger.Log("DRY-RUN", "=== Simulação (Dry Run) Concluída com Sucesso! ===");
                AorusLogger.Log("DRY-RUN", "A porta foi aberta, o handshake IAP (0xF1) respondeu perfeitamente e NENHUM dado foi alterado na Flash.");
                transport.Close();
                return (true, "Dry Run concluído com sucesso!\n\nDispositivo detectado e Handshake IAP confirmado.\nNenhum dado foi escrito ou apagado na memória Flash.");
            }

            // Se NÃO for Dry Run (desativado por restrição na etapa de testes)
            AorusLogger.Log("WARN", "Gravação real solicitada. Iniciando etapas de gravação na Flash...");

            // Tarefa 8: Sem retentativas cegas. Em qualquer falha, abortar e informar o pacote exato.
            byte[] paddedData = rawBinData;
            if (rawBinData.Length % 2048 != 0)
            {
                int newLen = (rawBinData.Length / 2048 + 1) * 2048;
                paddedData = new byte[newLen];
                Array.Fill<byte>(paddedData, 0xFF);
                Array.Copy(rawBinData, paddedData, rawBinData.Length);
            }

            uint length = (uint)paddedData.Length;
            uint blockSize = BlockSize; // 64 KB

            // Apagamento de blocos
            uint totalErase = length;
            if (totalErase % blockSize != 0)
            {
                totalErase = (totalErase / blockSize + 1) * blockSize;
            }

            for (uint offset = 0; offset < totalErase; offset += blockSize)
            {
                ct.ThrowIfCancellationRequested();
                int pct = 40 + (int)((long)offset * 20 / totalErase);
                progress?.Report((pct, $"Apagando bloco 0x{offset:X} na Flash SPI... ({pct}%)"));

                AorusLogger.Log("FLASH", $"Apagando bloco Flash offset 0x{offset:X8} ({blockSize} bytes)...");
                if (!transport.SendScsiCommand(CMD_ERASE, 0, offset, blockSize, null))
                {
                    AorusLogger.Log("ERR", $"ERRO FATAL: Falha ao apagar bloco Flash no offset 0x{offset:X8}. Parando imediatamente sem retentativas.");
                    transport.Close();
                    return (false, $"Falha fatal ao apagar bloco Flash no offset 0x{offset:X8}. Operação abortada.");
                }
            }

            // Gravação de blocos
            int remaining = paddedData.Length;
            int srcOffset = 0;
            byte[] chunkBuf = new byte[blockSize];

            while (remaining > 0)
            {
                ct.ThrowIfCancellationRequested();
                int pct = 60 + (int)((long)srcOffset * 35 / paddedData.Length);
                progress?.Report((pct, $"Gravando pacote offset 0x{srcOffset:X}... ({pct}%)"));

                uint writeLen = (uint)Math.Min(remaining, blockSize);
                Array.Copy(paddedData, srcOffset, chunkBuf, 0, (int)writeLen);

                AorusLogger.Log("FLASH", $"Gravando pacote no offset 0x{srcOffset:X8} ({writeLen} bytes)...");
                if (!transport.SendScsiCommand(CMD_WRITE, 64, (uint)srcOffset, writeLen, chunkBuf))
                {
                    AorusLogger.Log("ERR", $"ERRO FATAL: Falha ao gravar pacote no offset 0x{srcOffset:X8}. Parando imediatamente sem retentativas.");
                    transport.Close();
                    return (false, $"Falha fatal ao gravar bloco de imagem no offset 0x{srcOffset:X8}. Operação abortada.");
                }

                srcOffset += (int)writeLen;
                remaining -= (int)writeLen;
            }

            // Retornar dispositivo ao modo normal (AP)
            progress?.Report((96, "Reiniciando Water Cooler para modo normal (AP)..."));
            transport.RebootDevice(0);
            transport.Close();

            await Task.Delay(1500, ct);
        }

        AorusLogger.Log("FLOW", "Gravação concluída com sucesso e handle liberado.");
        progress?.Report((100, "✓ Gravação concluída!"));
        return (true, "Gravação concluída com sucesso! O dispositivo foi reiniciado no modo normal e o descritor USB foi liberado.");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
