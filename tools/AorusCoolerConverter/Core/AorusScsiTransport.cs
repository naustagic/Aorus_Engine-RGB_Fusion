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

        return _deviceHandle != null && !_deviceHandle.IsInvalid;
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
