using System;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
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
    private const byte CMD_TEST = 241;
    private const byte CMD_GET_ID = 242;
    private const byte CMD_WRITE = 243;
    private const byte CMD_READ = 244;
    private const byte CMD_ERASE = 245;
    private const byte CMD_ALTERNATE_MODE = 246;

    public const uint BlockSize = 65536; // 64 KB SPI Flash Block Size

    private SafeFileHandle? _deviceHandle;
    private bool _disposed;

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
    /// </summary>
    public static AorusDeviceInfo DetectDevice()
    {
        var info = new AorusDeviceInfo();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            foreach (ManagementObject drive in searcher.Get())
            {
                string caption = drive["Caption"]?.ToString() ?? "";
                if (caption.Contains("LCD_PLATFORM", StringComparison.OrdinalIgnoreCase))
                {
                    info.DevicePath = drive["DeviceID"]?.ToString() ?? "";
                    info.ModelName = caption;
                    break;
                }
            }
        }
        catch
        {
            // Non-windows or WMI service exception
        }

        return info;
    }

    public int LastWin32Error { get; private set; }

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
            return false;
        }

        LastWin32Error = 0;
        return true;
    }

    public void Close()
    {
        if (_deviceHandle != null && !_deviceHandle.IsClosed)
        {
            _deviceHandle.Dispose();
            _deviceHandle = null;
        }
    }

    public bool SendScsiCommand(byte cmd, byte type, uint offset, uint len, byte[]? buffer)
    {
        if (_deviceHandle == null || _deviceHandle.IsInvalid || _deviceHandle.IsClosed)
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

        lock (_deviceHandle)
        {
            ok = DeviceIoControl(_deviceHandle, IOCTL_SCSI_PASS_THROUGH_DIRECT, structPtr, structSize, structPtr, structSize, ref bytesReturned, IntPtr.Zero);
        }

        Marshal.PtrToStructure(structPtr, wrapper);

        if (buffer != null && dataPtr != IntPtr.Zero)
        {
            Marshal.Copy(dataPtr, buffer, 0, buffer.Length);
            Marshal.FreeHGlobal(dataPtr);
        }

        Marshal.FreeHGlobal(structPtr);
        return ok && wrapper.sptd.ScsiStatus == 0;
    }

    /// <summary>
    /// Tests device status via SCSI CMD 241 (0xF1).
    /// </summary>
    public uint TestDevice()
    {
        byte[] array = new byte[32];
        if (SendScsiCommand(CMD_TEST, 192, 0, (uint)array.Length, array))
        {
            return BitConverter.ToUInt32(array, 0);
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
        if (num != 0 && (num & 0x80000000u) == 0x80000000u)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Reboots device to IAP mode (mode=1) or normal operating mode (mode=0).
    /// </summary>
    public bool RebootDevice(byte mode)
    {
        uint num = TestDevice();
        return SendScsiCommand(CMD_ALTERNATE_MODE, 0, mode, mode, null);
    }

    /// <summary>
    /// Flashes a .BIN binary image directly into the LCD SPI Flash memory of the Aorus Cooler.
    /// Handles IAP mode rebooting, USB re-enumeration, block erase, and block writing.
    /// </summary>
    public static async Task<(bool Success, string Message)> FlashPayloadToCoolerAsync(
        byte[] rawBinData,
        IProgress<(int Percent, string Status)>? progress = null,
        System.Threading.CancellationToken ct = default)
    {
        if (rawBinData == null || rawBinData.Length == 0)
        {
            return (false, "Nenhum dado binário fornecido para gravação.");
        }

        // 1. Detectar dispositivo
        progress?.Report((5, "Detectando Water Cooler via USB..."));
        var dev = DetectDevice();
        if (!dev.IsFound)
        {
            return (false, "Water Cooler ('LCD_PLATFORM') não encontrado. Verifique a conexão USB.");
        }

        // 2. Conectar e verificar se precisa reiniciar em IAP
        using (var transport = new AorusScsiTransport())
        {
            if (!transport.OpenDevice(dev.DevicePath))
            {
                if (transport.LastWin32Error == 5)
                {
                    return (false, "Acesso Negado (Erro 5): O aplicativo precisa ser Executado como Administrador para acessar a porta USB/SCSI do Water Cooler.");
                }
                return (false, $"Não foi possível abrir a porta de comunicação com o Water Cooler (Erro Win32: {transport.LastWin32Error}).");
            }

            if (!transport.IsIapMode())
            {
                progress?.Report((10, "Reiniciando Water Cooler em Modo IAP..."));
                transport.RebootDevice(1);
                transport.Close();

                // Aguardar reinicialização USB do microcontrolador
                bool reconnected = false;
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(1000, ct);
                    progress?.Report((10 + i * 2, $"Aguardando reconexão IAP... ({i + 1}s)"));
                    var checkDev = DetectDevice();
                    if (checkDev.IsFound)
                    {
                        if (transport.OpenDevice(checkDev.DevicePath) && transport.IsIapMode())
                        {
                            reconnected = true;
                            break;
                        }
                        transport.Close();
                    }
                }

                if (!reconnected)
                {
                    return (false, "O Water Cooler reiniciou, mas o modo IAP não respondeu a tempo.");
                }
            }

            // 3. Preparar buffer alinhado a blocos de 2048 bytes
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

            // 4. Apagar blocos de memória Flash SPI externa
            uint totalErase = length;
            if (totalErase % blockSize != 0)
            {
                totalErase = (totalErase / blockSize + 1) * blockSize;
            }

            for (uint offset = 0; offset < totalErase; offset += blockSize)
            {
                ct.ThrowIfCancellationRequested();
                int pct = 25 + (int)((long)offset * 25 / totalErase);
                progress?.Report((pct, $"Apagando Flash SPI... ({pct}%)"));

                if (!transport.SendScsiCommand(CMD_ERASE, 0, offset, blockSize, null))
                {
                    return (false, $"Falha ao apagar bloco de memória Flash na posição 0x{offset:X}.");
                }
            }
            transport.TestDevice();

            // 5. Gravar dados binários na Flash SPI
            int remaining = paddedData.Length;
            int srcOffset = 0;
            byte[] chunkBuf = new byte[blockSize];

            while (remaining > 0)
            {
                ct.ThrowIfCancellationRequested();
                int pct = 50 + (int)((long)srcOffset * 45 / paddedData.Length);
                progress?.Report((pct, $"Gravando no LCD... ({pct}%)"));

                uint writeLen = (uint)Math.Min(remaining, blockSize);
                Array.Copy(paddedData, srcOffset, chunkBuf, 0, (int)writeLen);

                if (!transport.SendScsiCommand(CMD_WRITE, 64, (uint)srcOffset, writeLen, chunkBuf))
                {
                    return (false, $"Falha ao gravar bloco de imagem na posição 0x{srcOffset:X}.");
                }

                srcOffset += (int)writeLen;
                remaining -= (int)writeLen;
            }
            transport.TestDevice();

            // 6. Retornar dispositivo ao modo normal (AP) e liberar conexão USB
            progress?.Report((96, "Reiniciando Water Cooler para modo normal (AP)..."));
            transport.RebootDevice(0);
            transport.Close();

            // Aguardar a reinicialização USB do microcontrolador e liberação completa da porta
            await Task.Delay(1500, ct);
            for (int i = 0; i < 5; i++)
            {
                progress?.Report((97 + i, $"Confirmando inicialização do display... ({i + 1}s)"));
                var checkDev = DetectDevice();
                if (checkDev.IsFound)
                {
                    if (transport.OpenDevice(checkDev.DevicePath))
                    {
                        bool isIap = transport.IsIapMode();
                        transport.Close();
                        if (!isIap)
                        {
                            break;
                        }
                    }
                }
                await Task.Delay(1000, ct);
            }
        }

        progress?.Report((100, "✓ Gravação concluída! Water Cooler pronto e liberado."));
        return (true, "Gravação concluída com sucesso! O dispositivo foi reiniciado no modo normal e a porta USB foi liberada.");
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
