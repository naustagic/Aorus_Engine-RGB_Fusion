using System;
using System.Runtime.InteropServices;

internal class Win32
{
	[StructLayout(LayoutKind.Sequential)]
	public class DEV_BROADCAST_DEVICEINTERFACE
	{
		public int dbcc_size;

		public int dbcc_devicetype;

		public int dbcc_reserved;

		public Guid dbcc_classguid;

		public short dbcc_name;
	}

	public const int WM_DEVICECHANGE = 537;

	public const int DBT_DEVICEARRIVAL = 32768;

	public const int DBT_DEVICEREMOVECOMPLETE = 32772;

	public const int DBT_DEVNODES_CHANGED = 7;

	public const int DEVICE_NOTIFY_WINDOW_HANDLE = 0;

	public const int DEVICE_NOTIFY_SERVICE_HANDLE = 1;

	public const int DBT_DEVTYP_DEVICEINTERFACE = 5;

	public static Guid GUID_DEVINTERFACE_HID = new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");

	[DllImport("user32.dll", SetLastError = true)]
	public static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr NotificationFilter, int Flags);

	[DllImport("kernel32.dll")]
	public static extern int GetLastError();
}
