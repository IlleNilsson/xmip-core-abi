using Microsoft.Extensions.Configuration;

namespace Xmip.Surface.Test;

/// <summary>
/// A System Process declares its name, its location and its purpose through
/// the node, and takes the declaration away where it ends (ADR-0053 clause 3).
/// The file itself is <c>xmip-core-node</c>'s and tested there; these prove
/// that a .NET process declares through it and reads it back through it.
/// </summary>
public sealed class ProcessDeclarationTest
{
    [Fact]
    public void ADeclarationStandsWhileHeldAndIsGoneWhenDisposed()
    {
        ProcessDeclaration? declared = ProcessDeclaration.Declare(
            "xmip-surface-test", @"D:\a ""b""\C1-snapshot.toml", "test");

        Assert.NotNull(declared);

        try
        {
            Abi.Operate.ProcessStanding standing = Assert.Single(
                ProcessDeclaration.Standing(Path.GetDirectoryName(declared.File)).Processes,
                process => process.File == declared.File);

            Assert.Equal("xmip-surface-test", standing.Name);
            Assert.Equal(@"D:\a ""b""\C1-snapshot.toml", standing.Location);
            Assert.Equal("test", standing.Purpose);
            Assert.Equal(Environment.ProcessId, standing.Pid);
        }
        finally
        {
            declared.Dispose();
        }

        Assert.False(File.Exists(declared.File), "taken away where the process ends");
    }

    [Fact]
    public void APurposeThatIsNoWordIsRefusedByTheNode()
    {
        // Until 2026-09-27 anything but test was taken for runtime.
        ArgumentException refused = Assert.Throws<ArgumentException>(
            () => ProcessDeclaration.Declare("xmip-surface-test", string.Empty, "production"));

        Assert.StartsWith("REFUSED", refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("test", "test")]
    [InlineData("runtime", "runtime")]
    [InlineData("production", "production")]
    [InlineData(null, "runtime")]
    [InlineData("", "runtime")]
    public void TheConfigurationStatesThePurposeAndNothingStatedIsRuntime(
        string? stated, string purpose)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                [new KeyValuePair<string, string?>(ProcessDeclaration.PurposeKey, stated)])
            .Build();

        Assert.Equal(purpose, ProcessDeclaration.PurposeOf(configuration));
    }
}
