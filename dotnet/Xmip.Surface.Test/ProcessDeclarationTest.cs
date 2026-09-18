using Microsoft.Extensions.Configuration;

namespace Xmip.Surface.Test;

/// <summary>
/// A System Process says its name, its location and its purpose to one file
/// named for it and its pid, and takes the file away where it ends
/// (ADR-0053 clause 3). The same file the Rust processes write.
/// </summary>
public sealed class ProcessDeclarationTest
{
    [Fact]
    public void ADeclarationStandsWhileHeldAndIsGoneWhenDisposed()
    {
        string directory = Path.Combine(
            Path.GetTempPath(), $"xmip-declaration-{Guid.NewGuid():n}");

        try
        {
            ProcessDeclaration declared = ProcessDeclaration.DeclareIn(
                directory,
                "xmip-gui-web",
                @"D:\a ""b""\C1-snapshot.toml",
                ProcessDeclaration.Test);
            string text = File.ReadAllText(declared.File);

            Assert.EndsWith(
                $"xmip-gui-web-{Environment.ProcessId}.toml",
                declared.File,
                StringComparison.Ordinal);
            Assert.Contains("name = \"xmip-gui-web\"", text, StringComparison.Ordinal);
            Assert.Contains(
                "location = \"D:\\\\a \\\"b\\\"\\\\C1-snapshot.toml\"",
                text,
                StringComparison.Ordinal);
            Assert.Contains("purpose = \"test\"", text, StringComparison.Ordinal);
            Assert.Contains($"pid = {Environment.ProcessId}", text, StringComparison.Ordinal);

            declared.Dispose();
            Assert.False(File.Exists(declared.File), "taken away where the process ends");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("test", "test")]
    [InlineData(" TEST ", "test")]
    [InlineData("runtime", "runtime")]
    [InlineData("production", "runtime")]
    [InlineData(null, "runtime")]
    public void AProcessIsRuntimeUnlessTheConfigurationSaysTest(string? stated, string purpose)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                [new KeyValuePair<string, string?>(ProcessDeclaration.PurposeKey, stated)])
            .Build();

        Assert.Equal(purpose, ProcessDeclaration.PurposeOf(configuration));
    }
}
