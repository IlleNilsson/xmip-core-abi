using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeJourneys"/> crosses section 16 to the runtime this
/// estate built and back (runtime-model.md section 13; ADR-0013): no node
/// runs in a test's process, so a Retry or a Dismiss on a node is refused in
/// words and a word that is no act on a Journey is invalid; and either act is
/// left as an order on the noun journey where a publication says, no other
/// act taken on it. What a Retry sends again is the runtime's and tested
/// there; these prove the crossing.
/// </summary>
public sealed class RuntimeJourneysTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's sending node, by what it declares.
    private static readonly string Sender = Cluster.WithRole("sending");
    private static readonly string Node = $"{Cluster.Scope}/node/{Sender}";

    private static RuntimeRules Rules => RuntimeRulesTest.Rules;

    [Theory]
    [InlineData("retry")]
    [InlineData("dismiss")]
    public void NoNodeRunsHereSoAnActIsRefused(string act)
    {
        Assert.Equal(
            XmipStatus.NotFound, Rules.Journeys.Act(Node, "j-1", act, "ilian", out string said));
        Assert.StartsWith("REFUSED", said, StringComparison.Ordinal);
    }

    [Fact]
    public void AWordThatIsNoActOnAJourneyIsInvalid()
    {
        Assert.Equal(
            XmipStatus.Invalid, Rules.Journeys.Act(Node, "j-1", "replay", "ilian", out string said));
        Assert.NotEmpty(said);
    }

    [Fact]
    public void AnActIsLeftAsAnOrderAndNoOtherActIsTaken()
    {
        string orders = Path.Combine(Path.GetTempPath(), $"xmip-abi-journey-{Guid.NewGuid():n}");

        try
        {
            foreach (string act in new[] { "retry", "dismiss" })
            {
                Assert.Equal(
                    XmipStatus.Ok,
                    Rules.Subscriptions.Order(
                        orders, Node, "journey", "j-1", act, "ilian", out string file));
                Assert.True(File.Exists(file), file);
                Assert.StartsWith(Path.Combine(orders, Sender), file, StringComparison.Ordinal);
            }

            Assert.Equal(
                XmipStatus.Invalid,
                Rules.Subscriptions.Order(
                    orders, Node, "journey", "j-1", "replay", "ilian", out string said));
            Assert.Contains("retry", said, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(orders, recursive: true);
        }
    }
}
