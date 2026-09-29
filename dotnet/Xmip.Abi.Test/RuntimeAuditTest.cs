using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeAudit"/> crosses section 9 of <c>xmip_operate.h</c> both
/// ways to the runtime this estate built. The record, its reader and its query
/// are tested in Rust, where they are written (<c>xmip-core-audit</c>); these
/// prove the crossing: a record made from .NET is read back whole, and a
/// refusal is carried as the capability's sentence.
/// </summary>
public sealed class RuntimeAuditTest
{
    [Fact]
    public void ARecordMadeFromDotNetIsReadBackWhole()
    {
        string directory = Path.Combine(
            Path.GetTempPath(), $"xmip-abi-audit-read-{Environment.ProcessId}");
        RuntimeAudit audit = RuntimeRulesTest.Rules.Audit;

        try
        {
            audit.Record(
                "xmip-abi-test", directory, "probe", AuditPhase.Failure, AuditSeverity.Error,
                "a failure", new Dictionary<string, string> { ["why"] = "a test" });

            AuditRead read = audit.Read(
                directory, [new("severity", "error"), new("sort", "program")]);

            Assert.Equal(1, read.Read);
            AuditEntry entry = Assert.Single(read.Records);
            Assert.Equal("xmip-abi-test", entry.Program);
            Assert.Equal("failure", entry.Phase);
            Assert.Equal("a test", entry.Properties["why"]);
            Assert.Null(entry.Location);
            Assert.Equal("a failure", entry.Summary);
            Assert.Equal("host", Assert.Single(read.Groups).Kind);
            Assert.Equal(["probe"], read.Actions);
            Assert.Equal("at", read.Columns[0]);
            Assert.Equal(["information", "warning", "error"], read.Severities);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AQueryTheCapabilityDoesNotTakeIsRefusedInItsWords()
    {
        ArgumentException refused = Assert.Throws<ArgumentException>(() =>
            RuntimeRulesTest.Rules.Audit.Read(null, [new("colour", "red")]));

        Assert.StartsWith("REFUSED", refused.Message, StringComparison.Ordinal);
    }
}
