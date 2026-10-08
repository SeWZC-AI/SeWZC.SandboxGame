using System.Runtime.InteropServices;

internal static class BenchmarkClock
{
    internal static long ReadThreadCpu()
    {
        if (!OperatingSystem.IsLinux()) return 0;
        if (clock_gettime(3, out var time) != 0) throw new InvalidOperationException("无法读取线程 CPU 时钟。");
        return time.Seconds * 1_000_000_000 + time.Nanoseconds;
    }

    [DllImport("libc")]
    private static extern int clock_gettime(int clock, out Timespec time);

    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec { internal long Seconds; internal long Nanoseconds; }
}
