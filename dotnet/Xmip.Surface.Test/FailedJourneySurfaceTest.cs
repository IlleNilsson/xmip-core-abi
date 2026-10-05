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
    public void AScopeNamesItsNodeAndItsSendPort()
    {
        Assert.Equal((Sending, "invoices"), JourneyOperation.PortAt($"{Sending}/send/invoices"));
        Assert.Equal((Sending, string.Empty), JourneyOperation.PortAt(Sending));
        Assert.Equal((string.Empty, string.Empty), JourneyOperation.PortAt(Cluster.Scope));
    }
}
