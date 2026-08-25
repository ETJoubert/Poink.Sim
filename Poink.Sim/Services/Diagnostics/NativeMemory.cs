using System;
using System.Runtime.InteropServices;

namespace Poink.Sim.Diagnostics;

internal static class NativeMemory
{
	[StructLayout(LayoutKind.Sequential)]
	private struct MEMORYSTATUSEX
	{
		public uint dwLength;
		public uint dwMemoryLoad;
		public ulong ullTotalPhys;
		public ulong ullAvailPhys;
		public ulong ullTotalPageFile;
		public ulong ullAvailPageFile;
		public ulong ullTotalVirtual;
		public ulong ullAvailVirtual;
		public ulong ullAvailExtendedVirtual;
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

	/// <summary>
	/// Reports physical memory via the Windows-only kernel32 export. Non-Windows hosts (e.g. the
	/// Linux container build) get zero for both values rather than an exception.
	/// </summary>
	public static (ulong totalBytes, ulong availBytes) GetPhysicalMemory()
	{
		if (!OperatingSystem.IsWindows())
			return (0UL, 0UL);

		var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
		return GlobalMemoryStatusEx(ref status) ? (status.ullTotalPhys, status.ullAvailPhys) : (0UL, 0UL);
	}
}
