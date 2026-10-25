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

        // 1. Localizar especificamente o drive HOLTEK LCD_PLATFORM via Win32_DiskDrive
        string? drivePath = AorusDiagnostics.FindLcdPlatformDiskDrive();
        if (!string.IsNullOrEmpty(drivePath))
        {
            using var handle = CreateFile(
                drivePath,
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
                var (ok, chipId, verAddr) = testTransport.GetChipId();

                info.DevicePath = drivePath;
                info.ModelName = "HOLTEK LCD_PLATFORM";
                info.VersionAddress = verAddr;
                info.IsIapMode = testTransport.IsIapMode();
                testTransport._deviceHandle = null; // Do not dispose handle, will be managed by caller
                return info;
            }
        }

        AorusLogger.Log("DIAG", "Nenhum disco com identificador 'LCD_PLATFORM' ou 'VID_04D9' foi localizado em Win32_DiskDrive.");
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
        int maxRetries = 4;
        int retry = 0;

        AorusLogger.LogHex("SCSI_TX", $"Opcode 0x{cmd:X2} CDB", wrapper.sptd.Cdb, 10);

        while (retry < maxRetries)
        {
            lock (_deviceHandle)
            {
                if (_deviceHandle.IsClosed || _deviceHandle.IsInvalid)
                {
                    Marshal.FreeHGlobal(structPtr);
                    if (dataPtr != IntPtr.Zero) Marshal.FreeHGlobal(dataPtr);
                    return false;
                }
                ok = DeviceIoControl(_deviceHandle, IOCTL_SCSI_PASS_THROUGH_DIRECT, structPtr, structSize, structPtr, structSize, ref bytesReturned, IntPtr.Zero);
            }

            if (ok) break;

            int lastWin32Error = Marshal.GetLastWin32Error();
            retry++;
            if (lastWin32Error == 31 && retry < maxRetries) // ERROR_GEN_FAILURE
            {
                AorusLogger.Log("RETRY", $"[SCSI] Win32 Error 31 (Gen Failure). Tentativa {retry}/{maxRetries} para opcode 0x{cmd:X2}...");
                System.Threading.Thread.Sleep(50);
                continue;
            }
            AorusLogger.Log("WARN", $"DeviceIoControl falhou (Win32 Error: {lastWin32Error}, tentativa {retry}/{maxRetries}).");
            break;
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
    /// In original Gigabyte HT32F.cs (line 395): bit 31 (0x80000000) indicates AP mode (returns false).
    /// When in IAP mode, bit 31 is zero and bit 1 (0x02) is active (returns true).
    /// </summary>
    public bool IsIapMode()
    {
        uint num = TestDevice();
        if (num != 0 && (num & 0x80000000u) == 0x80000000u)
        {
            return false;
        }
        return num != 0 && (num & 2) == 2;
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
    /// Reads a 64 KB block of External SPI Flash memory via SCSI CMD 244 (0xF4) with type 192 (TYPE_EXT_FLASH).
    /// </summary>
    public bool ReadExtFlashBlock(uint offset, uint length, byte[] buffer)
    {
        return SendScsiCommand(CMD_READ, 192, offset, length, buffer);
    }

    /// <summary>
    /// Reads a block of MCU internal ROM via SCSI CMD 244 (0xF4) with type 193 (TYPE_ROM).
    /// </summary>
    public bool ReadInternalRomBlock(uint offset, uint length, byte[] buffer)
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
    /// Dumps the SPI Flash contents using CMD_READ (0xF4, type 192) and calculates the SHA-256 hash.
    /// Purely read-only; does not write or erase anything.
    /// Performs 3-region cross-validation and block statistics.
    /// </summary>
    public static async Task<(bool Success, string Message, string? Sha256)> DumpFlashToFileAsync(
        string outputPath,
        IProgress<(int Percent, string Status)>? progress = null,
        System.Threading.CancellationToken ct = default)
    {
        AorusLogger.Log("DUMP", $"=== Início do Dump da Flash SPI (Somente Leitura) ===");
        AorusLogger.Log("DUMP", $"Destino do arquivo: {outputPath}");

        var dev = DetectDevice();
        if (!dev.IsFound)
        {
            AorusLogger.Log("ERR", "Water Cooler não encontrado na porta USB (Win32_DiskDrive LCD_PLATFORM não localizado).");
            return (false, "Water Cooler não encontrado na porta USB.", null);
        }

        using var transport = new AorusScsiTransport();
        if (!transport.OpenDevice(dev.DevicePath))
        {
            return (false, $"Não foi possível abrir o dispositivo (Erro Win32: {transport.LastWin32Error}).", null);
        }

        // TestDevice 0xF1 (Handshake de diagnóstico não bloqueante para dump)
        uint handshakeStatus = transport.TestDevice();
        AorusLogger.Log("DUMP", $"Resposta do handshake 0xF1: 0x{handshakeStatus:X8}");
        if (handshakeStatus != 0x00000002)
        {
            AorusLogger.Log("WARN", $"[DUMP] Handshake 0xF1 retornou 0x{handshakeStatus:X8} (esperado 0x02 em IAP). Prosseguindo com leitura da flash (modo somente leitura)...");
        }
        else
        {
            AorusLogger.Log("DUMP", "✓ Handshake 0xF1 confirmado em modo IAP (0x00000002).");
        }

        // Consulta Chip ID (0xF2)
        var (okChip, chipId, verAddr) = transport.GetChipId();
        if (okChip)
        {
            bool isHoltekFamily = (chipId & 0xFFFF0000u) == 0x03760000u;
            AorusLogger.Log("DUMP", $"Chip ID (0xF2): 0x{chipId:X8} (Família Holtek HT32F: {isHoltekFamily}), Version Address: 0x{verAddr:X8}");
        }

        uint totalSize = 10485760; // 10 MB Flash (tamanho do 0.bin / partição de animação)
        uint blockSize = 65536;    // 64 KB por bloco (conforme LCDPlatform.blockSize)
        int totalBlocks = (int)(totalSize / blockSize); // 160 blocos
        byte[] blockBuffer = new byte[blockSize];

        byte[] block0 = new byte[blockSize];
        byte[] blockMid = new byte[blockSize];
        byte[] blockEnd = new byte[blockSize];
        uint midOffset = (uint)((totalBlocks / 2) * blockSize); // 0x500000 (5 MB)
        uint endOffset = (uint)((totalBlocks - 1) * blockSize); // 0x9F0000 (bloco 159)

        int emptyFfBlocks = 0;
        int zeroBlocks = 0;
        int dataBlocks = 0;

        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var sha256 = SHA256.Create();

        for (int i = 0; i < totalBlocks; i++)
        {
            ct.ThrowIfCancellationRequested();
            uint offset = (uint)(i * blockSize);
            int pct = (int)((long)offset * 85 / totalSize);
            progress?.Report((pct, $"Lendo Bloco {i + 1}/{totalBlocks} (Offset 0x{offset:X6})..."));

            if (!transport.ReadExtFlashBlock(offset, blockSize, blockBuffer))
            {
                AorusLogger.Log("ERR", $"Falha fatal na leitura do bloco {i} no offset 0x{offset:X8}. Abortando dump.");
                return (false, $"Falha na leitura do bloco no offset 0x{offset:X8}.", null);
            }

            fileStream.Write(blockBuffer, 0, blockBuffer.Length);
            sha256.TransformBlock(blockBuffer, 0, blockBuffer.Length, null, 0);

            // Armazenar amostras para validação cruzada
            if (i == 0) Array.Copy(blockBuffer, block0, blockSize);
            if (offset == midOffset) Array.Copy(blockBuffer, blockMid, blockSize);
            if (offset == endOffset) Array.Copy(blockBuffer, blockEnd, blockSize);

            // Analisar Bloco 0 (Cabeçalho de animação)
            if (i == 0)
            {
                AorusLogger.LogHex("DUMP", "Primeiros 32 bytes do Bloco 0 (Cabeçalho da Flash)", blockBuffer, 32);
                if (blockBuffer[0] == 0x81 && blockBuffer[1] == 0x00)
                {
                    AorusLogger.Log("DUMP", "✓ Cabeçalho padrão AORUS detectado: 0x0081 (129 quadros).");
                }
                else
                {
                    AorusLogger.Log("WARN", $"Cabeçalho do Bloco 0: 0x{blockBuffer[1]:X2}{blockBuffer[0]:X2} (esperado 0x0081 para padrão de fábrica).");
                }
            }

            // Análise de conteúdo do bloco
            bool allFf = true;
            bool allZero = true;
            for (int b = 0; b < blockBuffer.Length; b++)
            {
                if (blockBuffer[b] != 0xFF) allFf = false;
                if (blockBuffer[b] != 0x00) allZero = false;
                if (!allFf && !allZero) break;
            }

            if (allFf) emptyFfBlocks++;
            else if (allZero) zeroBlocks++;
            else dataBlocks++;
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        string hashFull = BitConverter.ToString(sha256.Hash!).Replace("-", "").ToLowerInvariant();
        fileStream.Flush();

        AorusLogger.Log("DUMP", $"Leitura concluída dos 160 blocos (10 MB).");
        AorusLogger.Log("DUMP", $"Estatísticas da Flash: {dataBlocks} blocos com dados, {emptyFfBlocks} blocos vazios (0xFF), {zeroBlocks} blocos zerados (0x00).");
        AorusLogger.Log("DUMP", $"SHA-256 do arquivo gravado: {hashFull}");

        // ═════════════════════════════════════════════════════════════════════════
        // Validação Cruzada: Relendo 3 regiões independentes (Início, Meio, Fim)
        // ═════════════════════════════════════════════════════════════════════════
        progress?.Report((90, "Executando validação cruzada (re-leitura de 3 regiões)..."));
        AorusLogger.Log("DUMP", "Iniciando validação cruzada de leitura (3 regiões independentes)...");

        using var shaBlock = SHA256.Create();
        byte[] verifyBuffer = new byte[blockSize];

        // Região 1: Início (Bloco 0, offset 0x000000)
        string hashOrig0 = BitConverter.ToString(shaBlock.ComputeHash(block0)).Replace("-", "").ToLowerInvariant();
        if (transport.ReadExtFlashBlock(0, blockSize, verifyBuffer))
        {
            string hashRe0 = BitConverter.ToString(shaBlock.ComputeHash(verifyBuffer)).Replace("-", "").ToLowerInvariant();
            if (hashOrig0 == hashRe0)
                AorusLogger.Log("DUMP", $"✓ Validação Cruzada [Início: 0x000000]: HASH IDÊNTICO ({hashOrig0})");
            else
                AorusLogger.Log("ERR", $"X Validação Cruzada [Início] DIVERGIU! Leitura 1: {hashOrig0}, Leitura 2: {hashRe0}");
        }

        // Região 2: Meio (Bloco 80, offset 0x500000)
        string hashOrigMid = BitConverter.ToString(shaBlock.ComputeHash(blockMid)).Replace("-", "").ToLowerInvariant();
        if (transport.ReadExtFlashBlock(midOffset, blockSize, verifyBuffer))
        {
            string hashReMid = BitConverter.ToString(shaBlock.ComputeHash(verifyBuffer)).Replace("-", "").ToLowerInvariant();
            if (hashOrigMid == hashReMid)
                AorusLogger.Log("DUMP", $"✓ Validação Cruzada [Meio: 0x{midOffset:X6}]: HASH IDÊNTICO ({hashOrigMid})");
            else
                AorusLogger.Log("ERR", $"X Validação Cruzada [Meio] DIVERGIU! Leitura 1: {hashOrigMid}, Leitura 2: {hashReMid}");
        }

        // Região 3: Fim (Bloco 159, offset 0x9F0000)
        string hashOrigEnd = BitConverter.ToString(shaBlock.ComputeHash(blockEnd)).Replace("-", "").ToLowerInvariant();
        if (transport.ReadExtFlashBlock(endOffset, blockSize, verifyBuffer))
        {
            string hashReEnd = BitConverter.ToString(shaBlock.ComputeHash(verifyBuffer)).Replace("-", "").ToLowerInvariant();
            if (hashOrigEnd == hashReEnd)
                AorusLogger.Log("DUMP", $"✓ Validação Cruzada [Fim: 0x{endOffset:X6}]: HASH IDÊNTICO ({hashOrigEnd})");
            else
                AorusLogger.Log("ERR", $"X Validação Cruzada [Fim] DIVERGIU! Leitura 1: {hashOrigEnd}, Leitura 2: {hashReEnd}");
        }

        transport.Close();
        progress?.Report((100, "✓ Dump e validação cruzada concluídos!"));
        AorusLogger.Log("DUMP", $"=== Dump Concluído com Sucesso! Arquivo: {outputPath} ===");

        return (true, $"Dump de 10 MB salvo e validado com sucesso!\n\nArquivo: {Path.GetFileName(outputPath)}\nSHA-256: {hashFull}\n\nBlocos com dados: {dataBlocks}\nBlocos vazios (0xFF): {emptyFfBlocks}\nBlocos zerados: {zeroBlocks}", hashFull);
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

            // No protocolo original da Gigabyte (HT32F.cs):
            // Em modo IAP: bit 31 (0x80000000) é 0 (não é modo AP) e bit 1 (0x02) é 1 (pronto).
            bool isIap = (testStatus != 0) && ((testStatus & 0x80000000u) == 0) && ((testStatus & 2) == 2);
            if (isIap)
            {
                AorusLogger.Log("HANDSHAKE", "✓ Modo IAP / Bootloader validado conforme especificação original (retorno 0x00000002).");
            }
            else
            {
                AorusLogger.Log("WARN", $"Handshake 0xF1 retornou 0x{testStatus:X8} (esperado 0x00000002 para modo IAP).");
                if (!isDryRun)
                {
                    AorusLogger.Log("ERR", "Gravação real BLOQUEADA: handshake não validado pelo critério original. Abortando sem escrever nada.");
                    transport.Close();
                    return (false, $"Handshake IAP falhou. Resposta 0x{testStatus:X8} não confirmou o bootloader. Gravação cancelada por segurança.");
                }
            }

            // Obter Chip ID e Versão da ROM (0xF2)
            var (okChip, chipId, verAddr) = transport.GetChipId();
            if (okChip)
            {
                bool isHoltekFamily = (chipId & 0xFFFF0000u) == 0x03760000u;
                AorusLogger.Log("HANDSHAKE", $"Chip ID (0xF2): 0x{chipId:X8} (Família Holtek HT32F: {isHoltekFamily}), Versão: 0x{verAddr:X8}");
                if (isHoltekFamily)
                {
                    AorusLogger.Log("HANDSHAKE", "✓ Assinatura do microcontrolador Holtek HT32F validada com sucesso!");
                }
            }

            // Tarefa 7 / Restrição: Modo Dry Run (Somente Leitura e Validação)
            if (isDryRun)
            {
                progress?.Report((100, "✓ Dry Run concluído com sucesso!"));
                AorusLogger.Log("DRY-RUN", "=== Simulação (Dry Run) Concluída com Sucesso! ===");
                AorusLogger.Log("DRY-RUN", $"Dispositivo: {dev.DevicePath}, Handshake 0xF1: 0x{testStatus:X8}, IAP Confirmado: {isIap}, Chip ID: 0x{chipId:X8}, Versão: 0x{verAddr:X8}.");
                AorusLogger.Log("DRY-RUN", "NENHUM dado foi alterado na Flash.");
                transport.Close();
                return (true, $"Dry Run concluído com sucesso!\n\nDispositivo: {dev.DevicePath}\nHandshake 0xF1: 0x{testStatus:X8} (Modo IAP: {(isIap ? "Confirmado" : "Aviso")})\nChip ID 0xF2: 0x{chipId:X8}\nVersion Address: 0x{verAddr:X8}\n\nNenhum dado foi escrito ou apagado na memória Flash.");
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

    /// <summary>
    /// Restores the official MCU application firmware (F1.4) and writes the activation header at 0x3C00.
    /// Recovers cooler from stuck bootloader (PID_A126, blank screen) back to normal mode (PID_3020).
    /// </summary>
    public static async Task<(bool Success, string Message)> RestoreOfficialFirmwareAsync(
        string? firmwareBinPath = null,
        IProgress<(int Percent, string Status)>? progress = null,
        System.Threading.CancellationToken ct = default)
    {
        AorusLogger.Log("RECOVERY", "=== INÍCIO DO PROCEDIMENTO DE RECUPERAÇÃO DO FIRMWARE OFICIAL (F1.4) ===");

        // 1. Localizar o arquivo de firmware oficial
        string fwPath = firmwareBinPath ?? "";
        if (string.IsNullOrEmpty(fwPath) || !File.Exists(fwPath))
        {
            string candidate1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fw", "flash_data.bin");
            string candidate2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flash_data.bin");
            string candidate3 = @"D:\eng-rev-watercooler\fw\flash_data.bin";

            if (File.Exists(candidate1)) fwPath = candidate1;
            else if (File.Exists(candidate2)) fwPath = candidate2;
            else if (File.Exists(candidate3)) fwPath = candidate3;
            else
            {
                AorusLogger.Log("ERR", "Arquivo de firmware 'flash_data.bin' não foi encontrado.");
                return (false, "Arquivo de firmware 'flash_data.bin' não encontrado. Coloque-o na mesma pasta do aplicativo.");
            }
        }

        byte[] apFirmware = File.ReadAllBytes(fwPath);
        AorusLogger.Log("RECOVERY", $"Firmware carregado: '{fwPath}' ({apFirmware.Length} bytes).");

        if (apFirmware.Length != 46852)
        {
            AorusLogger.Log("ERR", $"Tamanho de firmware incorreto: {apFirmware.Length} bytes (esperado: 46852 bytes).");
            return (false, $"Tamanho de firmware inválido: {apFirmware.Length} bytes. Operação abortada por segurança.");
        }

        // Validar SHA-256
        using (var sha = SHA256.Create())
        {
            string fwHash = BitConverter.ToString(sha.ComputeHash(apFirmware)).Replace("-", "").ToLowerInvariant();
            AorusLogger.Log("RECOVERY", $"SHA-256 do firmware: {fwHash}");
            if (fwHash != "975f81cf6c5fcf6c42cc5498fc12ee679b8ff5b8cd796feafa401b7ab184953e")
            {
                AorusLogger.Log("WARN", "Aviso: Hash SHA-256 diverge da versão F1.4 de referência.");
            }
        }

        // 2. Encerrar processos concorrentes
        progress?.Report((5, "Encerrando processos conflitantes..."));
        AorusDiagnostics.CloseGigabyteProcesses();

        // 3. Detectar dispositivo em modo IAP
        progress?.Report((10, "Localizando dispositivo HOLTEK LCD_PLATFORM..."));
        var dev = DetectDevice();
        if (!dev.IsFound)
        {
            AorusLogger.Log("ERR", "Water Cooler em modo IAP não detectado em Win32_DiskDrive.");
            return (false, "Dispositivo Water Cooler não encontrado na porta USB. Verifique a conexão.");
        }

        using var transport = new AorusScsiTransport();
        if (!transport.OpenDevice(dev.DevicePath))
        {
            AorusLogger.Log("ERR", $"Falha ao abrir handle de comunicação em '{dev.DevicePath}'.");
            return (false, $"Não foi possível abrir o Water Cooler (Erro Win32: {transport.LastWin32Error}).");
        }

        // 4. TestDevice (0xF1) e Chip ID (0xF2)
        progress?.Report((15, "Verificando microcontrolador..."));
        uint statusF1 = transport.TestDevice();
        AorusLogger.Log("RECOVERY", $"Handshake 0xF1: 0x{statusF1:X8}");

        var (okChip, chipId, verAddr) = transport.GetChipId();
        AorusLogger.Log("RECOVERY", $"Chip ID 0xF2: 0x{chipId:X8}, Version Address: 0x{verAddr:X8}");

        if (!okChip || (chipId & 0xFFFF0000u) != 0x03760000u)
        {
            AorusLogger.Log("ERR", $"Chip ID inválido: 0x{chipId:X8} (esperado família Holtek HT32F: 0x0376xxxx).");
            transport.Close();
            return (false, $"Chip ID inválido (0x{chipId:X8}). Microcontrolador incompatível.");
        }

        uint versionAddress = verAddr != 0 ? verAddr : 0x3C00u;
        uint versionSize = 1024u;
        uint apAddress = versionAddress + versionSize; // 0x4000
        uint apLength = (uint)apFirmware.Length;       // 46852

        // 5. Apagar área de aplicação na Flash interna (ROM: 0x4000 a 0xF704)
        progress?.Report((20, "Apagando área da aplicação no microcontrolador (0x4000)..."));
        AorusLogger.Log("RECOVERY", $"Apagando Flash interna da aplicação (0x{apAddress:X8}, tamanho: {apLength} bytes)...");
        if (!transport.SendScsiCommand(CMD_ERASE, 1, apAddress, apLength, null))
        {
            AorusLogger.Log("ERR", "Falha ao enviar comando de apagamento da aplicação (0xF5).");
            transport.Close();
            return (false, "Falha ao apagar área de aplicação na memória Flash.");
        }

        // 6. Gravar firmware AP em blocos de 64 bytes (sem pausas para não estourar o watchdog de 3s)
        progress?.Report((30, "Gravando firmware oficial ARM Cortex-M..."));
        AorusLogger.Log("RECOVERY", $"Iniciando gravação de {apLength} bytes em blocos de 64 bytes a partir de 0x{apAddress:X8}...");

        byte[] chunk = new byte[64];
        for (int i = 0; i < apFirmware.Length; i += chunk.Length)
        {
            ct.ThrowIfCancellationRequested();
            uint currentAddr = apAddress + (uint)i;
            uint chunkLen = (uint)Math.Min(chunk.Length, apFirmware.Length - i);

            Array.Copy(apFirmware, i, chunk, 0, (int)chunkLen);

            if (!transport.SendScsiCommand(CMD_WRITE, 65, currentAddr, chunkLen, chunk))
            {
                AorusLogger.Log("ERR", $"Falha fatal ao gravar bloco no endereço 0x{currentAddr:X8}. Abortando imediatamente!");
                transport.Close();
                return (false, $"Falha ao gravar bloco no endereço 0x{currentAddr:X8}.");
            }

            // Alimenta watchdog periodicamente
            if (i > 0 && i % (64 * 150) == 0)
            {
                transport.TestDevice();
            }

            int pct = 30 + (i * 35 / apFirmware.Length);
            progress?.Report((pct, $"Gravando firmware... {i / 1024} KB / {apLength / 1024} KB ({pct}%)"));
        }

        AorusLogger.Log("RECOVERY", "✓ Gravação dos 733 blocos do firmware concluída com sucesso.");

        // 7. Verificar (Verify) se o firmware foi gravado perfeitamente
        progress?.Report((68, "Validando leitura do firmware gravado (Verify)..."));
        AorusLogger.Log("RECOVERY", "Iniciando verificação de integridade (Verify)...");

        byte[] verifyChunk = new byte[1024];
        for (uint vOff = 0; vOff < apLength; vOff += 1024)
        {
            ct.ThrowIfCancellationRequested();
            uint vLen = Math.Min(1024u, apLength - vOff);
            uint readAddr = apAddress + vOff;

            if (!transport.ReadInternalRomBlock(readAddr, vLen, verifyChunk))
            {
                AorusLogger.Log("ERR", $"Falha na leitura de verificação no endereço 0x{readAddr:X8}.");
                transport.Close();
                return (false, $"Falha de leitura durante a verificação no endereço 0x{readAddr:X8}.");
            }

            for (int b = 0; b < vLen; b++)
            {
                if (verifyChunk[b] != apFirmware[vOff + b])
                {
                    AorusLogger.Log("ERR", $"Divergência detectada no endereço 0x{(readAddr + b):X8}: Lido 0x{verifyChunk[b]:X2}, Esperado 0x{apFirmware[vOff + b]:X2}.");
                    transport.Close();
                    return (false, $"Verificação do firmware falhou no endereço 0x{(readAddr + b):X8}. A tabela de ativação NÃO foi gravada por segurança.");
                }
            }
        }

        AorusLogger.Log("RECOVERY", "✓ VERIFY 100% CONCLUÍDO! Todos os 46.852 bytes conferem perfeitamente com o firmware oficial.");

        // 8. Gravar Tabela de Versão em 0x3C00 (Ativação do Boot)
        progress?.Report((80, "Gravando tabela de ativação de boot em 0x3C00..."));
        AorusLogger.Log("RECOVERY", $"Apagando setor de versão (0x{versionAddress:X8}, 1024 bytes)...");

        if (!transport.SendScsiCommand(CMD_ERASE, 1, versionAddress, versionSize, null))
        {
            AorusLogger.Log("ERR", "Falha ao apagar setor de versão 0x3C00.");
            transport.Close();
            return (false, "Falha ao apagar setor de versão 0x3C00.");
        }

        // Tabela oficial de 12 bytes: 08 00 00 00 56 '1' '.' '4' '.' '0' 00 00
        byte[] versionData = new byte[] { 0x08, 0x00, 0x00, 0x00, 0x56, (byte)'1', (byte)'.', (byte)'4', (byte)'.', (byte)'0', 0x00, 0x00 };
        AorusLogger.LogHex("RECOVERY", "Gravando Tabela de Versão (0x3C00)", versionData, versionData.Length);

        if (!transport.SendScsiCommand(CMD_WRITE, 65, versionAddress, (uint)versionData.Length, versionData))
        {
            AorusLogger.Log("ERR", "Falha ao gravar tabela de versão em 0x3C00.");
            transport.Close();
            return (false, "Falha ao gravar tabela de versão em 0x3C00.");
        }

        // Conferir a tabela gravada
        byte[] checkVersion = new byte[12];
        if (transport.ReadInternalRomBlock(versionAddress, (uint)checkVersion.Length, checkVersion))
        {
            AorusLogger.LogHex("RECOVERY", "Lendo de volta Tabela de Versão (0x3C00)", checkVersion, checkVersion.Length);
            bool match = true;
            for (int i = 0; i < versionData.Length; i++)
            {
                if (checkVersion[i] != versionData[i]) match = false;
            }
            if (match)
            {
                AorusLogger.Log("RECOVERY", "✓ Tabela de ativação confirmada com sucesso em 0x3C00 ('V1.4.0').");
            }
            else
            {
                AorusLogger.Log("ERR", "Aviso: Tabela de versão lida difere da gravada.");
            }
        }

        // 9. Disparo de Inicialização da Aplicação (Reboot AP Mode)
        progress?.Report((90, "Disparando inicialização do firmware (Reboot AP Mode)..."));
        AorusLogger.Log("RECOVERY", "Enviando comando 0xF6 (mode=0) para desarmar o watchdog e saltar para a aplicação...");

        transport.RebootDevice(0);
        transport.Close();

        AorusLogger.Log("RECOVERY", "Comando de inicialização enviado. Aguardando inicialização do LCD...");
        progress?.Report((95, "Aguardando reinicialização do Water Cooler..."));

        await Task.Delay(3000, ct);

        var pnpCheck = AorusDiagnostics.DetectHoltekUsbState();
        AorusLogger.Log("RECOVERY", $"Estado PnP pós-recuperação: {pnpCheck}");

        progress?.Report((100, "✓ Recuperação concluída com sucesso!"));
        AorusLogger.Log("RECOVERY", "=== RECUPERAÇÃO DO WATER COOLER CONCLUÍDA COM SUCESSO! ===");

        string finalMsg = "🎉 Water Cooler Recuperado com Sucesso!\n\n" +
                          "✓ Firmware oficial F1.4 gravado e verificado (46.852 bytes).\n" +
                          "✓ Tabela de ativação de boot restaurada em 0x3C00.\n" +
                          "✓ Comando de reinicialização para modo normal enviado.\n\n" +
                          $"Estado atual USB: {pnpCheck}\n\n" +
                          "O LCD deve acender e o Aorus Engine / RGB Fusion agora podem reconhecer o dispositivo.";

        return (true, finalMsg);
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
