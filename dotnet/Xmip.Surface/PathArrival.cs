namespace Xmip.Surface;

/// <summary>
/// Waits for a path a watch needs and does not have yet: a publication's
/// directory not made, a runtime library not built. A live view that began
/// before its source then comes alive the moment the source does (ADR-0052:
/// events, not polling). The wait is the file system's notice wherever there
/// is a directory to watch; only where not even the path's root is there —
/// a drive not mounted, a share not reachable — is it a retry, its interval
/// doubling to a bound, because nothing then exists to raise an event.
/// </summary>
internal static class PathArrival
{
    // The retry's first and longest interval, used only where no directory
    // on the path exists to watch.
    private static readonly TimeSpan FirstRetry = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan LongestRetry = TimeSpan.FromSeconds(5);

    /// <summary>Return once <paramref name="directory"/> exists: watching the
    /// nearest directory above it that does, a level at a time.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="stop"/>
    /// was cancelled first.</exception>
    public static async Task DirectoryAsync(string directory, CancellationToken stop)
    {
        string target = Path.GetFullPath(directory);
        TimeSpan retry = FirstRetry;

        while (!Directory.Exists(target))
        {
            stop.ThrowIfCancellationRequested();

            string? above = Nearest(target);
            if (above is null)
            {
                await Task.Delay(retry, stop).ConfigureAwait(false);
                retry = retry * 2 > LongestRetry ? LongestRetry : retry * 2;
                continue;
            }

            retry = FirstRetry;
            using Notice notice = new(above, "*", NotifyFilters.DirectoryName);

            // Attached before looking again: a directory made meanwhile is
            // either seen now or noticed.
            if (!Directory.Exists(target) && Nearest(target) == above)
            {
                await notice.Next(stop).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Return once the file at <paramref name="path"/> is other than
    /// <paramref name="seen"/> says — made, written or replaced — waiting for
    /// its directory first.
    /// </summary>
    /// <exception cref="OperationCanceledException"><paramref name="stop"/>
    /// was cancelled first.</exception>
    public static async Task ChangedAsync(string path, Stamp seen, CancellationToken stop)
    {
        string file = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(file) ?? file;

        while (true)
        {
            await DirectoryAsync(directory, stop).ConfigureAwait(false);

            using Notice notice = new(
                directory,
                Path.GetFileName(file),
                NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size);

            // Attached before comparing: a write racing the attach is either
            // seen now or noticed.
            if (Stamp.Of(file) != seen)
            {
                return;
            }

            await notice.Next(stop).ConfigureAwait(false);

            if (Stamp.Of(file) != seen)
            {
                return;
            }
        }
    }

    // The nearest directory above the target that exists; null where none
    // does, its root included.
    private static string? Nearest(string target)
    {
        for (string? at = Path.GetDirectoryName(target); at is not null;
            at = Path.GetDirectoryName(at))
        {
            if (Directory.Exists(at))
            {
                return at;
            }
        }

        return null;
    }

    /// <summary>A file as a wait saw it: whether it was there, when it was
    /// last written and how long it was.</summary>
    public readonly record struct Stamp(bool Exists, DateTime Written, long Length)
    {
        /// <summary>The file at <paramref name="path"/> as it is now.</summary>
        public static Stamp Of(string path)
        {
            FileInfo file = new(path);

            return file.Exists
                ? new Stamp(true, file.LastWriteTimeUtc, file.Length)
                : default;
        }
    }

    // One file-system watcher, its first notice as a task. A watcher whose
    // buffer overflowed has lost notices, which is a notice too.
    private sealed class Notice : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly TaskCompletionSource _noticed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Notice(string directory, string filter, NotifyFilters notify)
        {
            _watcher = new FileSystemWatcher(directory, filter) { NotifyFilter = notify };
            _watcher.Created += (_, _) => _noticed.TrySetResult();
            _watcher.Changed += (_, _) => _noticed.TrySetResult();
            _watcher.Renamed += (_, _) => _noticed.TrySetResult();
            _watcher.Deleted += (_, _) => _noticed.TrySetResult();
            _watcher.Error += (_, _) => _noticed.TrySetResult();
            _watcher.EnableRaisingEvents = true;
        }

        public Task Next(CancellationToken stop)
        {
            return _noticed.Task.WaitAsync(stop);
        }

        public void Dispose()
        {
            _watcher.Dispose();
        }
    }
}
