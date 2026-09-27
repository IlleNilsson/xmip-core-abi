using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// One follow for every face (ADR-0014 clause 10): the current answer first,
/// then a new one only when the publication advanced and the answer changed.
/// Until 2026-09-27 only the command line followed, with a loop of its own.
/// </summary>
public sealed class SurfaceFollowTest
{
    private static readonly string Fixture =
        Path.Combine(AppContext.BaseDirectory, "Fixture", "snapshot.toml");

    [Fact]
    public async Task TheCurrentAnswerComesFirstAndAChangedOneFollows()
    {
        string copy = Path.Combine(Path.GetTempPath(), $"xmip-follow-{Guid.NewGuid():N}.toml");
        File.Copy(Fixture, copy);

        try
        {
            SnapshotOperator surface = new(copy);
            ScopeSelection chosen = ScopeSelection.Exactly(ScopeTree.Root);
            using CancellationTokenSource patience = new(TimeSpan.FromSeconds(30));

            await using IAsyncEnumerator<IReadOnlyList<HealthRecord>> answers = SurfaceFollow
                .Changes(
                    surface,
                    chosen,
                    (read, now) => (IReadOnlyList<HealthRecord>)
                        [.. now.Scopes.SelectMany(read.Health)],
                    SurfaceFollow.Rows<HealthRecord>(),
                    patience.Token)
                .GetAsyncEnumerator(patience.Token);

            Assert.True(await answers.MoveNextAsync().ConfigureAwait(true));
            Assert.Equal(5, answers.Current.Count);

            File.WriteAllText(copy, "node = \"xmip:///edge-01\"\n");
            File.SetLastWriteTimeUtc(copy, DateTime.UtcNow.AddSeconds(5));

            Assert.True(await answers.MoveNextAsync().ConfigureAwait(true));
            Assert.Empty(answers.Current);
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Fact]
    public void RowsAreTheSameAnswerWhenTheyHoldEqualRowsInOrder()
    {
        IEqualityComparer<IReadOnlyList<string>> rows = SurfaceFollow.Rows<string>();

        Assert.True(rows.Equals(["a", "b"], ["a", "b"]));
        Assert.False(rows.Equals(["a", "b"], ["b", "a"]));
    }
}
