using System.Runtime.InteropServices;

namespace Poink.Sim.Diagnostics;

// GlobalMemoryStatusEx over a PerformanceCounter for RAM: it's a single
// synchronous call with no counter-category warm-up, and reports physical
// totals directly instead of needing "_Total"/"Available MBytes" pairing.
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

    public static (ulong totalBytes, ulong availBytes) GetPhysicalMemory()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref status) ? (status.ullTotalPhys, status.ullAvailPhys) : (0UL, 0UL);
    }
}
