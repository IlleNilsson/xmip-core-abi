using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The Messages that failed as a surface reading a publication lists them
/// (runtime-model.md section 13; <c>xmip_operate.h</c> section 16): each Send
/// Port's count and its Journeys with why, as the publication carries them,
/// drilled to a node or one Port by its scope and paged by place; and where
/// an act on one is left.
/// </summary>
public sealed class FailedMessageSurfaceTest : IDisposable
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's sending node, by what it declares, and another node.
    private static readonly string Sending = $"{Cluster.Scope}/node/{Cluster.WithRole("sending")}";
    private static readonly string Receiving =
        $"{Cluster.Scope}/node/{Cluster.WithRole("receiving")}";

    private readonly string scratch =
        Path.Combine(Path.GetTempPath(), $"xmip-failed-{Guid.NewGuid():N}");

    public FailedMessageSurfaceTest()
    {
        Directory.CreateDirectory(scratch);
    }

    public void Dispose()
    {
        Directory.Delete(scratch, recursive: true);
    }

    private SnapshotOperator Published()
    {
        string orders = Path.Combine(scratch, "orders").Replace('\\', '/');
        string text = $"""
            node = "{Cluster.Scope}"
            orders = "{orders}"

            [[failed_messages]]
            node = "{Sending}"
            send_port = "invoices"
            count = 3

            [[failed_messages.messages]]
            journey = "j-1"
            sequence = 2
            reason = "invoices: refused"

            [[failed_messages.messages]]
            journey = "j-2"
            sequence = 5
            reason = "invoices: refused again"

            [[failed_messages]]
            node = "{Sending}"
            send_port = "orders"
            count = 1

            [[failed_messages.messages]]
            journey = "j-9"
            sequence = 1
            reason = "orders: the share did not answer"

            [[failed_messages]]
            node = "{Receiving}"
            send_port = "audit"
            count = 1

            [[failed_messages.messages]]
            journey = "j-7"
            sequence = 4
            reason = "audit: refused"
            """;
        string path = Path.Combine(scratch, "snapshot.toml");
        File.WriteAllText(path, text);

        return new SnapshotOperator(path);
    }

    [Fact]
    public void ThePortsAtOrBeneathAScopeAreListedWithTheirCountAndWhy()
    {
        SnapshotOperator surface = Published();

        FailedMessageList cluster = surface.FailedMessages(Cluster.Scope);
        Assert.Equal(3, cluster.Ports.Count);

        FailedMessageList node = surface.FailedMessages(Sending);
        Assert.Equal(["invoices", "orders"], node.Ports.Select(port => port.SendPort));

        FailedMessagePort port = Assert.Single(
            surface.FailedMessages($"{Sending}/send/invoices").Ports);
        Assert.Equal((3ul, (ulong?)null), (port.Count, port.Next));
        Assert.Equal(["j-1", "j-2"], port.Messages.Select(journey => journey.Journey));
        FailedMessageRecord first = port.Messages[0];
        Assert.Equal(
            (Sending, "invoices", 2ul, "invoices: refused"),
            (first.Node, first.SendPort, first.Sequence, first.Reason));
    }

    [Fact]
    public void APageStartsAtItsPlaceAndHoldsAtMostItsCount()
    {
        SnapshotOperator surface = Published();

        FailedMessagePort paged = Assert.Single(
            surface.FailedMessages($"{Sending}/send/invoices", from: 3, most: 1).Ports);

        Assert.Equal(["j-2"], paged.Messages.Select(journey => journey.Journey));
        Assert.Equal(3ul, paged.Count);
    }

    [Fact]
    public void APublicationAnswersEvenWhereItListsNoneAndNoPublicationIsNoAnswer()
    {
        SnapshotOperator surface = Published();

        FailedMessageList none = surface.FailedMessages($"{Sending}/send/nothing");
        Assert.True(none.Listed);
        Assert.Empty(none.Messages);

        FailedMessageList unread = new SnapshotOperator(
            Path.Combine(scratch, "not-published-yet.toml")).FailedMessages(Cluster.Scope);
        Assert.False(unread.Listed);
        Assert.False(FailedMessageList.Unlisted.Listed);
    }

    [Fact]
    public void StorageThatDoesNotAnswerIsSaidAsAFailureNotThrownAndNotUnlisted()
    {
        string port = $"{Sending}/send/invoices";

        FailedMessageList failed = MessageOperation.Read(
            port, () => throw new InvalidOperationException("Xmip Storage is not open"));

        Assert.False(failed.Listed);
        Assert.Empty(failed.Messages);
        Assert.NotSame(FailedMessageList.Unlisted, failed);
        Assert.Equal(
            $"FAILED: the Messages that failed at {port} could not be read: "
                + "Xmip Storage is not open",
            failed.Failure);
        Assert.Empty(FailedMessageList.Unlisted.Failure);

        SnapshotOperator surface = Published();
        FailedMessageList answered =
            MessageOperation.Read(port, () => surface.FailedMessages(port));
        Assert.True(answered.Listed);
        Assert.Empty(answered.Failure);

        // Said in one sentence on every surface: none for an answer.
        Assert.Empty(MessageOperation.Unlisted(answered, port, surface.Source));
        Assert.Equal(failed.Failure, MessageOperation.Unlisted(failed, port, "here"));
        Assert.StartsWith(
            "NOT LISTED: here cannot list",
            MessageOperation.Unlisted(FailedMessageList.Unlisted, port, "here"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheFailedMessagesInAPortsQueueNowAreReadFromItsEvidence()
    {
        Assert.Equal(
            2ul,
            MessageOperation.FailingIn(
                "sent 4, failed 3, waiting 0, failed in its queue 2; the Message of the "
                + "Journey j-1 failed: x"));
        Assert.Equal(
            0ul,
            MessageOperation.FailingIn(
                "sent 4, failed 3, waiting 0, failed in its queue 0; the Message of the "
                + "Journey j-1 failed: x"));
        Assert.Null(MessageOperation.FailingIn("sent 4, failed 3, waiting 0"));
        Assert.Null(MessageOperation.FailingIn(null));
    }

    [Fact]
    public void AScopeNamesItsNodeAndItsSendPort()
    {
        Assert.Equal((Sending, "invoices"), MessageOperation.PortAt($"{Sending}/send/invoices"));
        Assert.Equal((Sending, string.Empty), MessageOperation.PortAt(Sending));
        Assert.Equal((string.Empty, string.Empty), MessageOperation.PortAt(Cluster.Scope));
    }
}
