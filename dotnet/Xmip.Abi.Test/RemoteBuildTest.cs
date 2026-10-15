using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// The runtime's remote build, the phone's (ADR-0014, amendment
/// 2026-10-14), loads: its rules, publication read, client and security
/// roles answer, and a node's binding is refused in words, never a crash
/// (<see cref="RemoteBuild"/>).
/// </summary>
public sealed class RemoteBuildTest
{
    private static readonly Lazy<RuntimeRules> Loaded = new(() =>
    {
        string library = Built();

        Assert.True(
            File.Exists(library),
            $"no remote build at {library}: at the estate's root in PowerShell, " +
            "Import-Module -Name ./Xmip/Xmip.psd1; Build-XmipService -Remote Desktop");

        return RuntimeRules.Load(library, out string reason)
            ?? throw new InvalidOperationException(reason);
    });

    private static RuntimeRules Rules => Loaded.Value;

    [Fact]
    public void TheRemoteBuildLoadsAndItsRulesAnswer()
    {
        Assert.NotEmpty(Rules.StageWords);
        Assert.NotEmpty(Rules.RoleWords);

        string word = Rules.Word(HealthState.Paused)
            ?? throw new InvalidOperationException("no word for Paused");

        Assert.Equal(HealthState.Paused, Rules.Named(word));
    }

    [Fact]
    public void TheRemoteBuildCarriesTheSecurityRoles()
    {
        Assert.Equal(1, Rules.SecurityRoles.Named(" Operator "));
        Assert.Equal(("Developer", "also builds"), Rules.SecurityRoles.Words(2));
        Assert.False(Rules.SecurityRoles.Admitted(null, 2, "pause", "x", out _));
    }

    [Fact]
    public void ANodesBindingIsRefusedInWords()
    {
        InvalidOperationException refused =
            Assert.Throws<InvalidOperationException>(() => Rules.Events);

        Assert.StartsWith(RemoteBuild.Sentence, refused.Message, StringComparison.Ordinal);
        Assert.Contains(
            RuntimeEvents.Entrypoints[0], refused.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => Rules.Design);
        Assert.Throws<InvalidOperationException>(() => Rules.Journeys);
    }

    [Fact]
    public void TheRemoteBuildMakesAPersonsRequestAndSignsNone()
    {
        // The phone makes its own key and request (ADR-0034, amendment
        // 2026-10-15); only the authority's node signs.
        Assert.True(
            Rules.Certificates.Request("alice@example.org", out _, out string request, out _));

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => Rules.Certificates.Sign("node.toml", request, out _, out _));

        Assert.StartsWith(RemoteBuild.Sentence, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRemoteBuildCarriesTheAudit()
    {
        // Every surface, the phone's too, records its own start and acts
        // (ProgramAudit), so section 9 is always there.
        Assert.NotNull(Rules.Audit);
    }

    [Fact]
    public void ARemoteBuildServesNothingAndSaysSo()
    {
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => Rules.Remotes.ServeLocal("xmip-test", "operator", "Xmip.Abi.Test", out _));

        Assert.StartsWith(RemoteBuild.Sentence, refused.Message, StringComparison.Ordinal);
    }

    // The estate's remote build, in the working area at its root.
    private static string Built()
    {
        DirectoryInfo? at = new(AppContext.BaseDirectory);

        while (at is not null && !Directory.Exists(Path.Combine(at.FullName, ".ai-interaction")))
        {
            at = at.Parent;
        }

        string root = at?.FullName ?? AppContext.BaseDirectory;

        return Path.Combine(
            root, ".ai-interaction", "target-remote", "debug", NativeRuntime.FileName);
    }
}
