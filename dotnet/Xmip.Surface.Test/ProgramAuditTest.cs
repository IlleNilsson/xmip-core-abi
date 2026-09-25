using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// A .NET program audits through the capability (ADR-0062): into the
/// directory it was told and, when the capability cannot be reached at all,
/// one entry of its own that says why. No test here writes to the
/// operating system's log.
/// </summary>
public sealed class ProgramAuditTest
{
    private static string Scratch()
    {
        return Path.Combine(Path.GetTempPath(), $"xmip-surface-audit-{Guid.NewGuid():n}");
    }

    [Fact]
    public void AnActLandsInTheDirectoryTheProgramWasTold()
    {
        string directory = Scratch();
        ProgramAudit audit = new("Xmip.Surface.Test", directory);

        AuditOutcome outcome = audit.Record(
            "start", AuditPhase.Begin, AuditSeverity.Information, "started",
            new Dictionary<string, string> { ["url"] = "http://127.0.0.1:5087" });

        Assert.Equal(AuditKept.Persisted, outcome.Kept);
        string text = File.ReadAllText(Path.Combine(directory, "audit.toml"));
        Assert.Contains("program = \"Xmip.Surface.Test\"", text, StringComparison.Ordinal);
        Assert.Contains("action = \"start\"", text, StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void AFailureCarriesTheExceptionTypeAndItsDetailOnce()
    {
        string directory = Scratch();
        ProgramAudit audit = new("Xmip.Surface.Test", directory);

        InvalidOperationException boom = new("boom");
        AuditOutcome? outcome = audit.Failed("unhandled", boom);

        Assert.Equal(AuditKept.Persisted, outcome?.Kept);
        Assert.Null(audit.Failed("host", boom));
        string text = File.ReadAllText(Path.Combine(directory, "audit.toml"));
        Assert.Contains("phase = \"failure\"", text, StringComparison.Ordinal);
        Assert.Contains("severity = \"error\"", text, StringComparison.Ordinal);
        Assert.Contains("message = \"boom\"", text, StringComparison.Ordinal);
        Assert.Contains(
            "\"exception\" = \"System.InvalidOperationException\"", text, StringComparison.Ordinal);
        Assert.Single(text.Split("[[record]]", StringSplitOptions.RemoveEmptyEntries));
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public void AProgramThatCannotReachAuditSaysWhyFirst()
    {
        // The sentence, not the entry: no test writes to this machine's log.
        // The writing is proved by hand, as ADR-0062's record of 2026-09-25
        // shows, and the capability's own fallback is tested in Rust.
        string said = OperatingSystemLog.Sentence(
            "Xmip.Surface.Test", "probe", AuditPhase.Failure, AuditSeverity.Error,
            "written by Xmip.Surface's own test",
            "no runtime library at a path this test names");

        Assert.StartsWith(
            "Xmip audit could not be reached (no runtime library at a path this test names), " +
            "so Xmip.Surface.Test writes this itself. probe failure error: written by",
            said,
            StringComparison.Ordinal);
        Assert.Contains($"pid {Environment.ProcessId}", said, StringComparison.Ordinal);
    }
}
