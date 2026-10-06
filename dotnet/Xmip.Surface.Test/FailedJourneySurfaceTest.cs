using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The Journeys that failed as a surface reading a publication lists them
/// (runtime-model.md section 13; <c>xmip_operate.h</c> section 16): each Send
/// Port's count and its Journeys with why, as the publication carries them,
/// drilled to a node or one Port by its scope and paged by place; and where
/// an act on one is left.
/// </summary>
public sealed class FailedJourneySurfaceTest : IDisposable
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's sending node, by what it declares, and another node.
    private static readonly string Sending = $"{Cluster.Scope}/node/{Cluster.WithRole("sending")}";
    private static readonly string Receiving =
        $"{Cluster.Scope}/node/{Cluster.WithRole("receiving")}";

    private readonly string scratch =
        Path.Combine(Path.GetTempPath(), $"xmip-failed-{Guid.NewGuid():N}");

    public FailedJourneySurfaceTest()
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

            [[failed_journeys]]
            node = "{Sending}"
            send_port = "invoices"
            count = 3

            [[failed_journeys.journeys]]
            journey = "j-1"
            sequence = 2
            reason = "invoices: refused"

            [[failed_journeys.journeys]]
            journey = "j-2"
            sequence = 5
            reason = "invoices: refused again"

            [[failed_journeys]]
            node = "{Sending}"
            send_port = "orders"
            count = 1

            [[failed_journeys.journeys]]
            journey = "j-9"
            sequence = 1
            reason = "orders: the share did not answer"

            [[failed_journeys]]
            node = "{Receiving}"
            send_port = "audit"
            count = 1

            [[failed_journeys.journeys]]
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

        FailedJourneyList cluster = surface.FailedJourneys(Cluster.Scope);
        Assert.Equal(3, cluster.Ports.Count);

        FailedJourneyList node = surface.FailedJourneys(Sending);
        Assert.Equal(["invoices", "orders"], node.Ports.Select(port => port.SendPort));

        FailedJourneyPort port = Assert.Single(
            surface.FailedJourneys($"{Sending}/send/invoices").Ports);
        Assert.Equal((3ul, (ulong?)null), (port.Count, port.Next));
        Assert.Equal(["j-1", "j-2"], port.Journeys.Select(journey => journey.Journey));
        FailedJourneyRecord first = port.Journeys[0];
        Assert.Equal(
            (Sending, "invoices", 2ul, "invoices: refused"),
            (first.Node, first.SendPort, first.Sequence, first.Reason));
    }

    [Fact]
    public void APageStartsAtItsPlaceAndHoldsAtMostItsCount()
    {
        SnapshotOperator surface = Published();

        FailedJourneyPort paged = Assert.Single(
            surface.FailedJourneys($"{Sending}/send/invoices", from: 3, most: 1).Ports);

        Assert.Equal(["j-2"], paged.Journeys.Select(journey => journey.Journey));
        Assert.Equal(3ul, paged.Count);
    }

    [Fact]
    public void APublicationAnswersEvenWhereItListsNoneAndNoPublicationIsNoAnswer()
    {
        SnapshotOperator surface = Published();

        FailedJourneyList none = surface.FailedJourneys($"{Sending}/send/nothing");
        Assert.True(none.Listed);
        Assert.Empty(none.Journeys);

        FailedJourneyList unread = new SnapshotOperator(
            Path.Combine(scratch, "not-published-yet.toml")).FailedJourneys(Cluster.Scope);
        Assert.False(unread.Listed);
        Assert.False(FailedJourneyList.Unlisted.Listed);
    }

    [Fact]
    public void ARuntimeWithNoNodeRunningInItsProcessCannotSayNoneFailed()
    {
        // Until 2026-10-06 a loaded runtime with no node here answered
        // Listed with no Port: an authoritative none it could not know.
        using NativeOperator surface = new(
            Path.Combine(RuntimeLibrary.Beside, RuntimeLibrary.FileName));
        Assert.True(surface.IsLoaded, surface.Reason);

        Assert.All(
            [Cluster.Scope, Sending, $"{Sending}/send/invoices"],
            scope =>
            {
                FailedJourneyList failed = surface.FailedJourneys(scope);

                Assert.False(failed.Listed, scope);
                Assert.Empty(failed.Failure);
                Assert.StartsWith(
                    "NOT LISTED: ",
                    JourneyOperation.Unlisted(failed, scope, surface.Source),
                    StringComparison.Ordinal);
            });
    }

    [Fact]
    public void StorageThatDoesNotAnswerIsSaidAsAFailureNotThrownAndNotUnlisted()
    {
        string port = $"{Sending}/send/invoices";

        FailedJourneyList failed = JourneyOperation.Read(
            port, () => throw new InvalidOperationException("Xmip Storage is not open"));

        Assert.False(failed.Listed);
        Assert.Empty(failed.Journeys);
        Assert.NotSame(FailedJourneyList.Unlisted, failed);
        Assert.Equal(
            $"FAILED: the Journeys that failed at {port} could not be read: "
                + "Xmip Storage is not open",
            failed.Failure);
        Assert.Empty(FailedJourneyList.Unlisted.Failure);

        SnapshotOperator surface = Published();
        FailedJourneyList answered = JourneyOperation.Read(port, () => surface.FailedJourneys(port));
        Assert.True(answered.Listed);
        Assert.Empty(answered.Failure);

        // Said in one sentence on every surface: none for an answer.
        Assert.Empty(JourneyOperation.Unlisted(answered, port, surface.Source));
        Assert.Equal(failed.Failure, JourneyOperation.Unlisted(failed, port, "here"));
        Assert.StartsWith(
            "NOT LISTED: here cannot list",
            JourneyOperation.Unlisted(FailedJourneyList.Unlisted, port, "here"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheFailedJourneysInAPortsQueueNowAreReadFromItsEvidence()
    {
        Assert.Equal(
            2ul,
            JourneyOperation.FailingIn(
                "sent 4, failed 3, waiting 0, failed in its queue 2; the Journey j-1 failed: x"));
        Assert.Equal(
            0ul,
            JourneyOperation.FailingIn(
                "sent 4, failed 3, waiting 0, failed in its queue 0; the Journey j-1 failed: x"));
        Assert.Null(JourneyOperation.FailingIn("sent 4, failed 3, waiting 0"));
        Assert.Null(JourneyOperation.FailingIn(null));
    }

    [Fact]
    public void AScopeNamesItsNodeAndItsSendPort()
    {
        Assert.Equal((Sending, "invoices"), JourneyOperation.PortAt($"{Sending}/send/invoices"));
        Assert.Equal((Sending, string.Empty), JourneyOperation.PortAt(Sending));
        Assert.Equal((string.Empty, string.Empty), JourneyOperation.PortAt(Cluster.Scope));
    }
}
