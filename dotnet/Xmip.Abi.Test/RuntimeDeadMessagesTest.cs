using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeDeadMessages"/> crosses section 15 to the runtime this
/// estate built and back (ADR-0052, amendment 2026-10-01): no node runs in a
/// test's process, so nothing is listed and a Replay on a node is refused in
/// words; a Replay is left as an order on the noun dead-message where a
/// publication says, and no other act is taken on it; and a publication's
/// Dead Message Queue entries are read with their pairs in the order written.
/// What a Replay routes is the runtime's and tested there; these prove the
/// crossing.
/// </summary>
public sealed class RuntimeDeadMessagesTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's receiving node, by what it declares.
    private static readonly string Receiver = Cluster.WithRole("receiving");
    private static readonly string Node = $"{Cluster.Scope}/node/{Receiver}";

    private static RuntimeRules Rules => RuntimeRulesTest.Rules;

    [Fact]
    public void NoNodeRunsHereSoNothingIsListedAndAReplayIsRefused()
    {
        Assert.Empty(Rules.DeadMessages.Standing(Node).DeadMessages);
        Assert.Equal(string.Empty, Rules.DeadMessages.Standing(string.Empty).Orders);

        Assert.Equal(
            XmipStatus.NotFound, Rules.DeadMessages.Replay(Node, "m-1", "ilian", out string said));
        Assert.StartsWith("REFUSED", said, StringComparison.Ordinal);
    }

    [Fact]
    public void AReplayIsLeftAsAnOrderAndNoOtherActIsTaken()
    {
        string orders = Path.Combine(Path.GetTempPath(), $"xmip-abi-dead-{Guid.NewGuid():n}");

        try
        {
            Assert.Equal(
                XmipStatus.Ok,
                Rules.Subscriptions.Order(
                    orders, Node, "dead-message", "m-1", "replay", "ilian", out string file));
            Assert.True(File.Exists(file), file);
            Assert.StartsWith(Path.Combine(orders, Receiver), file, StringComparison.Ordinal);

            Assert.Equal(
                XmipStatus.Invalid,
                Rules.Subscriptions.Order(
                    orders, Node, "dead-message", "m-1", "pause", "ilian", out string said));
            Assert.Contains("replay", said, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(orders, recursive: true);
        }
    }

    [Fact]
    public void APublicationsDeadMessagesAndItsOrdersAreRead()
    {
        string text =
            $"node = \"{Cluster.Scope}\"\norders = \"shared/orders\"\n"
            + $"[[dead_messages]]\nnode = \"{Node}\"\nmessage = \"m-1\"\nsequence = 4\n"
            + "location = \"orders\"\nreceived_unix_nanos = 5000000000\n"
            + "validation = [[\"schema\", \"valid\"]]\n"
            + "promoted = [[\"MessageType\", \"Invoice\"], [\"Party\", \"partner-x\"]]\n"
            + "declines = [[\"structured\", \"MessageType is Invoice\"]]\n";

        Publication read = Rules.Publications.Read(text, out _)
            ?? throw new InvalidOperationException("no publication");
        DeadMessageRecord kept = Assert.Single(read.DeadMessages.DeadMessages);

        Assert.Equal("shared/orders", read.DeadMessages.Orders);
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
