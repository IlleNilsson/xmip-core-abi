namespace Xmip.Surface.Test;

/// <summary>
/// A live view begun before its source recovers when the source arrives
/// (ADR-0052: events, not polling): a snapshot surface whose publication's
/// directory is not made yet says so at once and announces a change the
/// moment the file system notices the source made.
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
