using System.Text.Json;
using System.Text.Json.Nodes;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeReports"/> crosses section 25 of <c>xmip_operate.h</c>
/// to the runtime this estate built (ADR-0072, amendment 2026-11-10). The
/// list, each entry's kind and every refusal are tested in Rust, where they
/// are written (<c>xmip-core-report</c>'s <c>catalog</c> and <c>answer</c>);
/// these prove the crossing: the answer whole and in the header's shape, each
/// kind the header's number, and a refusal carried as the runtime's sentence.
/// </summary>
public sealed class RuntimeReportsTest
{
    private static RuntimeReports Reports => RuntimeRulesTest.Rules.Reports;

    [Fact]
    public void TheListComesBackAsTheHeadersJsonEachKindTheHeadersNumber()
    {
        string directory = Directory.CreateTempSubdirectory("xmip-abi-reports-").FullName;

        try
        {
            using JsonDocument listed = JsonDocument.Parse(Reports.Ask(
                "list", new JsonObject { ["directory"] = directory }.ToJsonString()));
            JsonElement[] kinds = [.. listed.RootElement.GetProperty("kinds").EnumerateArray()];
            JsonElement[] reports =
                [.. listed.RootElement.GetProperty("reports").EnumerateArray()];

            Assert.Equal(
                Enum.GetValues<ReportKind>().Select(kind => (int)kind),
                kinds.Select(kind => kind.GetProperty("kind").GetInt32()));
            Assert.All(kinds, kind => Assert.NotEmpty(kind.GetProperty("name").GetString()!));
            Assert.NotEmpty(reports);
            Assert.All(reports, report => Assert.True(Enum.IsDefined(
                (ReportKind)report.GetProperty("kind").GetInt32())));
            Assert.Contains(
                reports, report => report.GetProperty("kind").GetInt32() == (int)ReportKind.Quest
                    && report.GetProperty("available").GetBoolean());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AnAnswerLongerThanTheRoomIsAskedForAgainAndComesBackWhole()
    {
        string directory = Directory.CreateTempSubdirectory("xmip-abi-reports-room-").FullName;

        try
        {
            string arguments = new JsonObject { ["directory"] = directory }.ToJsonString();
            string whole = Reports.Ask("list", arguments);

            Assert.True(whole.Length > 16, whole);
            Assert.Equal(whole, Reports.Starting(16).Ask("list", arguments));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AProbeAskedAgainForRoomIsAuditedOnce()
    {
        TestCluster cluster = TestCluster.Read();
        string directory = Directory.CreateTempSubdirectory("xmip-abi-reports-once-").FullName;

        try
        {
            string listed = Reports.Ask(
                "list", new JsonObject { ["directory"] = directory }.ToJsonString());
            using JsonDocument list = JsonDocument.Parse(listed);
            JsonElement quest = list.RootElement.GetProperty("reports").EnumerateArray().First(
                report => report.GetProperty("kind").GetInt32() == (int)ReportKind.Quest
                    && report.GetProperty("available").GetBoolean());
            JsonObject arguments = new()
            {
                ["report"] = quest.GetProperty("name").GetString(),
                ["configuration"] = cluster.Path,
                ["node"] = cluster.WithRole("storage"),
                ["program"] = "Xmip.Abi.Test",
                ["directory"] = directory,
            };

            if (quest.TryGetProperty("parameter", out JsonElement parameter))
            {
                arguments["parameter"] =
                    parameter.GetProperty("choices").EnumerateArray().First().GetString();
            }

            using JsonDocument probed =
                JsonDocument.Parse(Reports.Starting(16).Ask("open", arguments.ToJsonString()));

            Assert.True(probed.RootElement.TryGetProperty("took_ms", out _));
            Assert.Equal(
                1,
                File.ReadAllLines(Path.Combine(directory, "audit.toml")).Count(
                    line => line.Contains("\"quest.probe\"", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ARefusalIsCarriedAsTheRuntimesSentence()
    {
        InvalidOperationException refused =
            Assert.Throws<InvalidOperationException>(() => Reports.Ask("nothing", "{}"));

        Assert.StartsWith("REFUSED:", refused.Message, StringComparison.Ordinal);
        Assert.Contains("\"nothing\"", refused.Message, StringComparison.Ordinal);
    }
}
