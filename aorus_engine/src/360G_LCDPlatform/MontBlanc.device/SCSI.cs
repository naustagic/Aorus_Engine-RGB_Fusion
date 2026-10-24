#define DEBUG
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MontBlanc.device;

internal class SCSI
{
	[StructLayout(LayoutKind.Sequential)]
	private class SCSI_PASS_THROUGH_DIRECT
	{
		public ushort Length = (ushort)Marshal.SizeOf(typeof(SCSI_PASS_THROUGH_DIRECT));

		public byte ScsiStatus = 0;

		public byte PathId = 0;

		public byte TargetId = 1;

		public byte Lun = 0;

		public byte CdbLength = 10;

		public byte SenseInfoLength = 24;

		public byte DataIn = 0;

		public uint DataTransferLength;

		public uint TimeOutValue = 100u;

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
			Filler = 0u;
			ucSenseBuf = new byte[4096];
		}
	}

	private static readonly int IOCTL_SCSI_PASS_THROUGH_DIRECT = 315412;

	private SafeFileHandle deviceHandle = null;

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern SafeFileHandle CreateFile(string lpFileName, [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess, [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode, IntPtr lpSecurityAttributes, [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition, [MarshalAs(UnmanagedType.U4)] FileAttributes dwFlagsAndAttributes, IntPtr hTemplateFile);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool DeviceIoControl(SafeFileHandle hDevice, int dwIoControlCode, IntPtr lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, int utBufferSize, ref int lpBytesReturned, IntPtr lpOverlapped);

	public bool isAvailable()
	{
		if (deviceHandle != null && !deviceHandle.IsInvalid)
		{
			return true;
		}
		return false;
	}

	public bool openDevice(string lpFileName)
	{
		deviceHandle = CreateFile(lpFileName, FileAccess.ReadWrite, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, FileAttributes.Normal, IntPtr.Zero);
		if (deviceHandle != null && !deviceHandle.IsInvalid)
		{
			return true;
		}
		deviceHandle.Dispose();
		return false;
	}

	public bool closeDevice()
	{
		if (deviceHandle != null)
		{
			deviceHandle.Dispose();
		}
		return true;
	}

	public bool sendCmd(byte cmd, byte type, uint offset, uint len, byte[] lpBuffer)
	{
		IntPtr intPtr = IntPtr.Zero;
		if (!isAvailable())
		{
			return false;
		}
		SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER sCSI_PASS_THROUGH_DIRECT_WITH_BUFFER = new SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER(len);
		sCSI_PASS_THROUGH_DIRECT_WITH_BUFFER.sptd.DataIn = (((type & 0x80) == 128) ? ((byte)1) : ((byte)0));
		if ((type & 0x40) == 64)
		{
			sCSI_PASS_THROUGH_DIRECT_WITH_BUFFER.sptd.DataTransferLength = len;
		}
		else
		{
			sCSI_PASS_THROUGH_DIRECT_WITH_BUFFER.sptd.DataTransferLength = 0u;
		}
		if (lpBuffer != null)
		{
			intPtr = Marshal.AllocHGlobal(lpBuffer.Length);
			Marshal.Copy(lpBuffer, 0, intPtr, lpBuffer.Length);
			sCSI_PASS_THROUGH_DIRECT_WITH_BUFFER.sptd.DataBuffer = intPtr;
		}
		sCSI_PASS_THROUGH_DIRECT_WITH_BUFFER.sptd.Cdb[0] = cmd;
		sCSI_PASS_THROUGH_DIRECT_WITH_BUFFER.sptd.Cdb[1] = (byte)(type & 1);
		sCSI_PASS_THROUGH_DIRECT_WITH_BUFFER.sptd.Cdb[2] = 0;
		byte[] bytes = BitConverter.GetBytes(offset);
		Array.Copy(bytes, 0, sCSI_PASS_THROUGH_DIRECT_WITH_BUFFER.sptd.Cdb, 3, 4);
		bytes = BitConverter.GetBytes(len);
		Array.Copy(bytes, 0, sCSI_PASS_THROUGH_DIRECT_WITH_BUFFER.sptd.Cdb, 7, 4);
		int lpBytesReturned = 0;
		int num = Marshal.SizeOf(typeof(SCSI_PASS_THROUGH_DIRECT_WITH_BUFFER));
		IntPtr intPtr2 = Marshal.AllocHGlobal(num);
		Marshal.StructureToPtr((object)sCSI_PASS_THROUGH_DIRECT_WITH_BUFFER, intPtr2, false);
		bool flag;
		lock (deviceHandle)
		{
			if (deviceHandle.IsClosed || deviceHandle.IsInvalid)
			{
				Marshal.FreeHGlobal(intPtr2);
				return false;
			}
			flag = DeviceIoControl(deviceHandle, IOCTL_SCSI_PASS_THROUGH_DIRECT, intPtr2, num, intPtr2, num, ref lpBytesReturned, IntPtr.Zero);
		}
		if (!flag)
		{
			int lastWin32Error = Marshal.GetLastWin32Error();
			Debug.WriteLine("!! Invalid {0}; Error ({1}): {2}", deviceHandle, lastWin32Error, new Win32Exception(lastWin32Error).Message);
			Debug.WriteLine("");
			while (lastWin32Error == 31 && !DeviceIoControl(deviceHandle, IOCTL_SCSI_PASS_THROUGH_DIRECT, intPtr2, num, intPtr2, num, ref lpBytesReturned, IntPtr.Zero))
			{
				lastWin32Error = Marshal.GetLastWin32Error();
			}
		}
		Marshal.PtrToStructure(intPtr2, (object)sCSI_PASS_THROUGH_DIRECT_WITH_BUFFER);
		if (lpBuffer != null)
		{
			Marshal.Copy(intPtr, lpBuffer, 0, lpBuffer.Length);
			Marshal.FreeHGlobal(intPtr);
		}
		Marshal.FreeHGlobal(intPtr2);
		if (sCSI_PASS_THROUGH_DIRECT_WITH_BUFFER.sptd.ScsiStatus != 0)
		{
			return false;
		}
		return true;
	}
}
