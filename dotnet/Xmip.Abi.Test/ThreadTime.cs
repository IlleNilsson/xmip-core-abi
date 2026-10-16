using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Xmip.Abi.Test;

/// <summary>
/// The processor time the calling thread has run, as the operating system
/// counts it: only while the thread runs, never while it waits for an event
/// or for a processor. So what a measurement of it says is the work the
/// thread did, and the same however busy the machine is — what a wall clock
/// across a wake cannot say, because a woken thread on a loaded machine
/// waits its turn (<see cref="EventLatencyTest"/>).
/// </summary>
/// <remarks>
/// On Windows the count is the thread's cycles (<c>QueryThreadCycleTime</c>),
/// which are never a duration by themselves; <see cref="Milliseconds"/>
/// turns them into one at the rate the cycles were seen to run at against
/// the wall clock over short spins, the fastest of twenty, so that a spin
/// a preemption cut short does not count. Elsewhere it is
/// <c>CLOCK_THREAD_CPUTIME_ID</c> in nanoseconds.
/// </remarks>
internal static class ThreadTime
{
    private const int LinuxThreadClock = 3;
    private const int MacThreadClock = 16;

    private static readonly Lazy<double> PerMillisecond = new(Calibrated);

    /// <summary>The calling thread's processor time now, in the platform's
    /// unit; only a difference of two is meaningful.</summary>
    public static long Now()
    {
        if (OperatingSystem.IsWindows())
        {
            return QueryThreadCycleTime(CurrentThread, out ulong cycles)
                ? (long)cycles
                : throw new InvalidOperationException("QueryThreadCycleTime failed.");
        }

        int clock = OperatingSystem.IsMacOS() ? MacThreadClock : LinuxThreadClock;

        return clock_gettime(clock, out TimeSpec now) == 0
            ? (now.Seconds * 1_000_000_000) + now.Nanoseconds
            : throw new InvalidOperationException("clock_gettime failed.");
    }

    /// <summary>A difference of two <see cref="Now"/>s in milliseconds.</summary>
    public static double Milliseconds(long spent)
    {
        return OperatingSystem.IsWindows() ? spent / PerMillisecond.Value : spent / 1e6;
    }

    // The cycles a thread runs a millisecond, the most seen over twenty
    // spins of one: a spin preempted counts fewer cycles for its time.
    private static double Calibrated()
    {
        double fastest = 0;

        for (int spin = 0; spin < 20; spin++)
        {
            long started = Stopwatch.GetTimestamp();
            long from = Now();

            while (Stopwatch.GetElapsedTime(started).TotalMilliseconds < 1.0)
            {
                Thread.SpinWait(10);
            }

            long to = Now();
            fastest = Math.Max(
                fastest, (to - from) / Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        return fastest;
    }

    // GetCurrentThread's pseudo-handle, which needs no closing.
    private static readonly nint CurrentThread = -2;

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

    [DllImport("libc", SetLastError = false)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
#pragma warning disable SA1300, IDE1006 // The C library's own name.
    private static extern int clock_gettime(int clock, out TimeSpec now);
#pragma warning restore SA1300, IDE1006
}
