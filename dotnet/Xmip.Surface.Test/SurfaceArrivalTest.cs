namespace Xmip.Surface.Test;

/// <summary>
/// A live view begun before its source recovers when the source arrives
/// (ADR-0052: events, not polling): a snapshot surface whose publication's
/// directory is not made yet, and a native surface whose runtime library is
/// not there yet or would not load, each say so at once and announce a
/// change the moment the file system notices the source made.
/// </summary>
public sealed class SurfaceArrivalTest : IDisposable
{
    private static readonly string Fixture =
        Path.Combine(AppContext.BaseDirectory, "Fixture", "snapshot.toml");

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly string _place =
        Path.Combine(Path.GetTempPath(), $"xmip-arrival-{Guid.NewGuid():N}");

    [Fact]
    public async Task ASnapshotWatchBegunBeforeItsDirectoryFollowsOnceItIsMade()
    {
        string directory = Path.Combine(_place, "not", "made");
        string path = Path.Combine(directory, "snapshot.toml");
        SnapshotOperator surface = new(path);
        using CancellationTokenSource stop = new(Patience);
        IAsyncEnumerator<SurfaceChange> follow =
            surface.WatchAsync(stop.Token).GetAsyncEnumerator(stop.Token);

        try
        {
            Assert.True(await follow.MoveNextAsync());
            Assert.Equal(0ul, follow.Current.Revision);
            Assert.Empty(surface.Health(ScopeTree.Root));

            ValueTask<bool> arrived = follow.MoveNextAsync();
            Directory.CreateDirectory(directory);
            File.Copy(Fixture, path + ".next");
            File.Move(path + ".next", path);

            // The directory's arrival is a change, and the file's another
            // where it lands after the watch attached.
            Assert.True(await arrived);
            while (surface.Health(ScopeTree.Root).Count == 0)
            {
                Assert.True(await follow.MoveNextAsync());
            }

            // And it follows from there, as any watch does.
            ValueTask<bool> next = follow.MoveNextAsync();
            File.Copy(Fixture, path + ".next");
            File.Move(path + ".next", path, overwrite: true);
            Assert.True(await next);
        }
        finally
        {
            await stop.CancelAsync();
            await follow.DisposeAsync();
        }
    }

    [Fact]
    public async Task ANativeWatchBegunBeforeItsRuntimeFollowsOnceTheRuntimeLoads()
    {
        string directory = Path.Combine(_place, "runtime");
        string library = Path.Combine(directory, RuntimeLibrary.FileName);
        using NativeOperator surface = new(library);
        using CancellationTokenSource stop = new(Patience);
        IAsyncEnumerator<SurfaceChange> follow =
            surface.WatchAsync(stop.Token).GetAsyncEnumerator(stop.Token);

        try
        {
            Assert.True(await follow.MoveNextAsync());
            Assert.False(surface.IsLoaded);

            // A file that will not load: read, it is refused and not tried
            // again on the next read — only once it is written again.
            ValueTask<bool> arrived = follow.MoveNextAsync();
            Directory.CreateDirectory(directory);
            File.WriteAllText(library + ".next", "not a library");
            File.Move(library + ".next", library);
            Assert.False(surface.IsLoaded);
            Assert.NotEmpty(surface.Reason);

            // Built again, it loads, and the watch says so.
            string built = Path.Combine(RuntimeLibrary.Beside, RuntimeLibrary.FileName);
            File.Copy(built, library + ".next");
            File.Move(library + ".next", library, overwrite: true);

            Assert.True(await arrived);
            Assert.True(surface.IsLoaded, surface.Reason);
        }
        finally
        {
            await stop.CancelAsync();
            await follow.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(_place, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
