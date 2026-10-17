using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Xmip.Surface.Test;

/// <summary>
/// The processor time a thread or this whole process has run, as the
/// operating system counts it: only while a thread runs, never while it
/// waits for an event or for a processor. So what a measurement of it says
/// is the work done, and the same however busy the machine is — what a
/// wall clock across a wake cannot say, because a woken thread on a loaded
/// machine waits its turn. One file, compiled into every .NET test project
/// that holds Xmip's own time to the millisecond rule (each links it).
/// </summary>
/// <remarks>
/// On Windows the count is cycles (<c>QueryThreadCycleTime</c>,
/// <c>QueryProcessCycleTime</c>), which are never a duration by themselves;
/// <see cref="Milliseconds"/> turns them into one at the rate the cycles were
/// seen to run at against the wall clock over short spins, the fastest of
/// twenty, so that a spin a preemption cut short does not count. Elsewhere it
/// is <c>CLOCK_THREAD_CPUTIME_ID</c> and <c>CLOCK_PROCESS_CPUTIME_ID</c> in
/// nanoseconds.
/// </remarks>
internal static class ProcessorTime
{
    private const int LinuxProcessClock = 2;
    private const int LinuxThreadClock = 3;
    private const int MacProcessClock = 12;
    private const int MacThreadClock = 16;

    // GetCurrentProcess's and GetCurrentThread's pseudo-handles, which need
    // no closing.
    private static readonly nint CurrentProcess = -1;
    private static readonly nint CurrentThread = -2;

    private static readonly Lazy<double> PerMillisecond = new(Calibrated);

    /// <summary>The calling thread's processor time now, in the platform's
    /// unit; only a difference of two is meaningful.</summary>
    public static long Thread()
    {
        if (OperatingSystem.IsWindows())
        {
            return QueryThreadCycleTime(CurrentThread, out ulong cycles)
                ? (long)cycles
                : throw new InvalidOperationException("QueryThreadCycleTime failed.");
        }

        return Clock(OperatingSystem.IsMacOS() ? MacThreadClock : LinuxThreadClock);
    }

    /// <summary>This process's processor time now, every thread of it
    /// summed, in the same unit as <see cref="Thread"/>; only a difference of
    /// two is meaningful.</summary>
    public static long Process()
    {
        if (OperatingSystem.IsWindows())
        {
            return QueryProcessCycleTime(CurrentProcess, out ulong cycles)
                ? (long)cycles
                : throw new InvalidOperationException("QueryProcessCycleTime failed.");
        }

        return Clock(OperatingSystem.IsMacOS() ? MacProcessClock : LinuxProcessClock);
    }

    /// <summary>A difference of two <see cref="Thread"/>s or of two
    /// <see cref="Process"/>es in milliseconds.</summary>
    public static double Milliseconds(long spent)
    {
        return OperatingSystem.IsWindows() ? spent / PerMillisecond.Value : spent / 1e6;
    }

    private static long Clock(int clock)
    {
        return clock_gettime(clock, out TimeSpec now) == 0
            ? (now.Seconds * 1_000_000_000) + now.Nanoseconds
            : throw new InvalidOperationException("clock_gettime failed.");
    }

    // The cycles a thread runs a millisecond, the most seen over twenty
    // spins of one: a spin preempted counts fewer cycles for its time.
    private static double Calibrated()
    {
        double fastest = 0;

        for (int spin = 0; spin < 20; spin++)
        {
            long started = Stopwatch.GetTimestamp();
            long from = Thread();

            while (Stopwatch.GetElapsedTime(started).TotalMilliseconds < 1.0)
            {
                System.Threading.Thread.SpinWait(10);
            }

            long to = Thread();
            fastest = Math.Max(
                fastest, (to - from) / Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        return fastest;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TimeSpec
    {
        public long Seconds;
        public long Nanoseconds;
    }

    [DllImport("kernel32", SetLastError = false)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryThreadCycleTime(nint thread, out ulong cycles);

    [DllImport("kernel32", SetLastError = false)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryProcessCycleTime(nint process, out ulong cycles);

    [DllImport("libc", SetLastError = false)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
#pragma warning disable SA1300, IDE1006 // The C library's own name.
    private static extern int clock_gettime(int clock, out TimeSpec now);
#pragma warning restore SA1300, IDE1006
}
