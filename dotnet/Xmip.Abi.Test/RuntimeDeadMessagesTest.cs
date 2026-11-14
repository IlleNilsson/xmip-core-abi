using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeDeadMessages"/> crosses section 15 to the runtime this
/// estate built and back (ADR-0052, amendment 2026-10-01): no node runs in a
/// test's process, so nothing is listed; and a publication's Dead Message
/// Queue entries are read with their pairs in the order written. What a
/// Replay routes is the runtime's and tested there, and a Replay is asked at
/// a node's door (ADR-0014, amendment 2026-11-13); these prove the crossing.
/// </summary>
public sealed class RuntimeDeadMessagesTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's receiving node, by what it declares.
    private static readonly string Receiver = Cluster.WithRole("receiving");
    private static readonly string Node = $"{Cluster.Scope}/node/{Receiver}";

    private static RuntimeRules Rules => RuntimeRulesTest.Rules;

    [Fact]
    public void NoNodeRunsHereSoNothingIsListed()
    {
        Assert.Empty(Rules.DeadMessages.Standing(Node).DeadMessages);
        Assert.Empty(Rules.DeadMessages.Standing(string.Empty).DeadMessages);
    }

    [Fact]
    public void APublicationsDeadMessagesAreRead()
    {
        string text =
            $"node = \"{Cluster.Scope}\"\n"
            + $"[[dead_messages]]\nnode = \"{Node}\"\nmessage = \"m-1\"\nsequence = 4\n"
            + "location = \"orders\"\nreceived_unix_nanos = 5000000000\n"
            + "validation = [[\"schema\", \"valid\"]]\n"
            + "promoted = [[\"MessageType\", \"Invoice\"], [\"Party\", \"partner-x\"]]\n"
            + "declines = [[\"structured\", \"MessageType is Invoice\"]]\n";

        Publication read = Rules.Publications.Read(text, out _)
            ?? throw new InvalidOperationException("no publication");
        DeadMessageRecord kept = Assert.Single(read.DeadMessages.DeadMessages);

        Assert.Equal(
            (Node, "m-1", 4ul, "orders"),
            (kept.Node, kept.Message, kept.Sequence, kept.ReceiveLocation));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(5), kept.Received);
        Assert.Equal([new("schema", "valid")], kept.Validation);
        Assert.Equal(
            [new("MessageType", "Invoice"), new("Party", "partner-x")], kept.Promoted);
        Assert.Equal([new("structured", "MessageType is Invoice")], kept.Declines);
        Assert.Empty(read.Subscriptions.Subscriptions);
    }
}
