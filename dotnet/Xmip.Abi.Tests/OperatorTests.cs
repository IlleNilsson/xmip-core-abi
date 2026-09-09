using Xmip.Abi.Operate;

namespace Xmip.Abi.Tests;

/// <summary>
/// <see cref="Operator.Load"/> answers null with a reason rather than throwing.
/// A surface that cannot reach a node shows that reason; an exception would
/// show a stack.
/// </summary>
public sealed class OperatorTests
{
    [Fact]
    public void LoadingAMissingPathAnswersNullWithTheReason()
    {
        string missing = Path.Combine(Path.GetTempPath(), "no-such-xmip-runtime.dll");

        Operator? loaded = Operator.Load(missing, out string reason);

        Assert.Null(loaded);
        Assert.Contains(missing, reason, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadingAFileThatIsNotALibraryAnswersNullWithTheReason()
    {
        string bogus = Path.Combine(
            Path.GetTempPath(),
            $"xmip-abi-test-{Guid.NewGuid():n}.dll");

        File.WriteAllText(bogus, "not a library");

        try
        {
            Operator? loaded = Operator.Load(bogus, out string reason);

            Assert.Null(loaded);
            Assert.Contains("could not be loaded", reason, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(bogus);
        }
    }
}
