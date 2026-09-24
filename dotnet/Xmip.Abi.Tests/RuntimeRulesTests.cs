using Xmip.Abi.Operate;

namespace Xmip.Abi.Tests;

/// <summary>
/// <see cref="RuntimeRules"/> crosses section 7 of <c>xmip_operate.h</c> to
/// the runtime this estate built — the library the binding's project copies
/// beside every test assembly — and brings back what the owning crate says.
/// The rules themselves are tested once, in Rust, where they are written
/// (<c>observe</c>'s <c>scope.rs</c> and <c>health.rs</c>, <c>node</c>'s
/// <c>stage.rs</c>); these prove the crossing: strings in and out, the fill
/// shape, a refusal carried whole and every mood.
/// </summary>
public sealed class RuntimeRulesTests
{
    private static readonly Lazy<RuntimeRules> Loaded = new(() =>
    {
        string[] beside = Directory.GetFiles(AppContext.BaseDirectory, "*xmip_core_runtime.*");

        Assert.True(
            beside.Length == 1,
            $"no runtime beside {AppContext.BaseDirectory}: cargo build in " +
            "module/platform/runtime, then build this project");

        return RuntimeRules.Load(beside[0], out string reason)
            ?? throw new InvalidOperationException(reason);
    });

    private static RuntimeRules Rules => Loaded.Value;

    [Theory]
    [InlineData("xmip:///n", "xmip:///n/receive/a", true)]
    [InlineData("xmip:///n", "xmip:///nx", false)]
    [InlineData("", "xmip:///n", true)]
    [InlineData("xmip://edge-01/n", "xmip:///n/receive", true)]
    [InlineData("xmip:///n/receive", "xmip:///n", false)]
    [InlineData("xmip:///ö/å", "xmip:///ö/å/ä", true)]
    public void ContainmentCrossesBothWays(string scope, string candidate, bool contains)
    {
        Assert.Equal(contains, Rules.Contains(scope, candidate));
    }

    [Fact]
    public void PartsComeBackAsTheSegmentsTopFirst()
    {
        Assert.Equal(
            ["edge-01", "receive", "orders"],
            Rules.Parts("xmip://lab:9000/edge-01/receive/orders/"));
        Assert.Equal(["ö", "å"], Rules.Parts("xmip:///ö/å"));
        Assert.Empty(Rules.Parts("xmip:///"));

        string deep = "xmip:///" + string.Join('/', Enumerable.Range(0, 40));

        Assert.Equal(40, Rules.Parts(deep).Length);
    }

    [Fact]
    public void TheStageWordsAndADeclarationCrossAndARefusalComesBackWhole()
    {
        Assert.Equal(["receive", "process", "send"], Rules.StageWords);
        Assert.Equal(["receive", "send"], Rules.Declared(" send + receive ", out string none));
        Assert.Empty(none);
        Assert.Empty(Rules.Declared(string.Empty, out _));

        Assert.Empty(Rules.Declared("Send+relay", out string refusal));
        Assert.Equal(
            "REFUSED: no capability is called Send, relay; a node declares receive, process, " +
            "send, or nothing at all.",
            refusal);

        string many = string.Join(',', Enumerable.Range(0, 100).Select(n => $"word{n}"));

        Assert.Empty(Rules.Declared(many, out string crowded));
        Assert.EndsWith("or nothing at all.", crowded, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryMoodCrossesWithAWordAColorAndBackAgain()
    {
        foreach (HealthState state in Enum.GetValues<HealthState>())
        {
            string? word = Rules.Word(state);

            Assert.NotNull(word);
            Assert.NotNull(Rules.Color(state));
            Assert.Equal(state, Rules.Named(word));
        }

        Assert.Null(Rules.Word((HealthState)42));
        Assert.Null(Rules.Color((HealthState)42));
        Assert.Null(Rules.Named("Fine"));
    }

    [Fact]
    public void ManyCrossInOneCallAndComeBackAsPositionsWorstFirst()
    {
        DateTimeOffset seen = DateTimeOffset.UnixEpoch;
        HealthRecord[] records =
        [
            new("xmip:///a", HealthState.Fine, 0, string.Empty, seen),
            new("xmip:///ö/d", HealthState.Done, 60, string.Empty, seen),
            new("xmip:///h", HealthState.Holding, 0, string.Empty, seen),
            new("xmip:///e", HealthState.Done, 90, string.Empty, seen),
        ];

        Assert.Equal([2, 3, 1, 0], Rules.WorstFirst(records));
        Assert.Empty(Rules.WorstFirst([]));
        Assert.Throws<InvalidOperationException>(() => Rules.WorstFirst(
            [new HealthRecord("xmip:///a", (HealthState)42, 0, string.Empty, seen)]));
    }

    [Fact]
    public void AMissingLibraryAnswersNullWithTheReason()
    {
        string missing = Path.Combine(Path.GetTempPath(), "no-such-xmip-runtime.dll");

        Assert.Null(RuntimeRules.Load(missing, out string reason));
        Assert.Contains(missing, reason, StringComparison.Ordinal);
    }
}
