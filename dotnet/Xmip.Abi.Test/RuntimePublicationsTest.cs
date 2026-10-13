using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="PublicationReader"/> crosses to the runtime this estate built
/// and brings back what the runtime reads from a publication and a curve,
/// and what each topology value is called. The reading is tested in Rust,
/// where it is written; these prove the crossing: a document in, its records
/// out whole, and a stranger refused.
/// </summary>
public sealed class RuntimePublicationsTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The receiving node; a name made from it that carries what is not ASCII
    // across; and the receiving node beneath its cluster.
    private static readonly string Receiver = Cluster.WithRole("receiving");
    private static readonly string Wide = $"{Receiver}ö";
    private static readonly string Node = $"{Cluster.Scope}/node/{Receiver}";

    private static PublicationReader Publications => RuntimeRulesTest.Rules.Publications;

    [Fact]
    public void APublicationCrossesAsTheRuntimeReadsItAndAStrangerIsRefused()
    {
        string text = $$"""
            source = "a publisher"
            node = "{{Cluster.Scope}}"

            [[records]]
            scope = "{{Node}}/receive/tcp"
            state = "done"
            severity = 90
            evidence = "refused"
            observed_unix_nanos = 1000

            [[records]]
            scope = "{{Node}}/send/tcp"
            state = "sulking"

            [[counts]]
            counted = "journeys"
            value = 4

            [[counts]]
            counted = "throughput"
            value = 1

            [run]
            cluster = "{{Cluster.Name}}"
            nodes = ["{{Receiver}}", "{{Wide}}"]
            roles = ["{{Receiver}}=receiving"]
            stress = "harsh"
            hidden = true

            [topology]
            observed_unix_nanos = 5

            [[topology.nodes]]
            id = "cluster"
            kind = "virtual-machine"
            origin = "configured"
            state = "fine"

            [[topology.links]]
            id = "l"
            from = "cluster"
            to = "cluster"
            pattern = "fire-and-forget"
            attempts = 3
            """;

        Publication read = Publications.Read(text, out string refusal)!;

        Assert.Empty(refusal);
        Assert.Equal(("a publisher", Cluster.Scope), (read.Source, read.Node));
        Assert.Equal(
            [HealthState.Done, HealthState.Stressed],
            read.Records.Select(record => record.State));
        Assert.Equal(90, read.Records[0].Severity);
        MeasurementRecord journeys = Assert.Single(read.Counts);
        Assert.Equal((Counted.Journeys, 4UL, Cluster.Scope),
            (journeys.Counted, journeys.Value, journeys.Scope));

        Assert.NotNull(read.Run);
        Assert.Equal([Receiver, Wide], read.Run.Nodes);
        Assert.Empty(read.Run.Tests);
        Assert.Equal("harsh", read.Run.Stress);
        Assert.True(read.Run.Hidden);

        Assert.NotNull(read.Topology);
        TopologyNode cluster = Assert.Single(read.Topology.Nodes);
        Assert.Equal(TopologyNodeKind.VirtualMachine, cluster.Kind);
        Assert.Equal(("cluster", (string?)null), (cluster.Label, cluster.ParentId));
        Assert.Equal("a publisher", read.Topology.Source);
        CommunicationLink link = Assert.Single(read.Topology.Links);
        Assert.Equal(
            (CommunicationPattern.FireAndForget, TopologyOrigin.Both, HealthState.Stressed, 3U),
            (link.Pattern, link.Origin, link.State, link.Attempts));

        Assert.Null(Publications.Read("not = [toml", out string said));
        Assert.NotEmpty(said);
        Publication bare = Publications.Read(string.Empty, out _)!;
        Assert.Null(bare.Run);
        Assert.Null(bare.Topology);
    }

    /// <summary>What each topology value is called crosses whole — every
    /// value its enum defines has a word and a name — and a value the runtime
    /// does not define has neither.</summary>
    [Fact]
    public void EveryTopologyValueCrossesWithItsWordAndName()
    {
        PublicationReader reader = Publications;

        Assert.All(
            Enum.GetValues<TopologyNodeKind>(), kind => Assert.NotNull(reader.Words(kind)));
        Assert.All(
            Enum.GetValues<TopologyOrigin>(), origin => Assert.NotNull(reader.Words(origin)));
        Assert.All(
            Enum.GetValues<CommunicationPattern>(),
            pattern => Assert.NotNull(reader.Words(pattern)));

        Assert.Equal(
            new TopologyWord("virtual-machine", "virtual machine"),
            reader.Words(TopologyNodeKind.VirtualMachine));
        Assert.Equal(
            new TopologyWord("both", "configured and observed"),
            reader.Words(TopologyOrigin.Both));
        Assert.Equal(
            new TopologyWord("publish-consume", "Publish → consume"),
            reader.Words(CommunicationPattern.PublishConsume));
        Assert.Null(reader.Words((TopologyNodeKind)99));
        Assert.Null(reader.Words((TopologyOrigin)(-1)));
        Assert.Null(reader.Words((CommunicationPattern)7));
    }

    [Fact]
    public void ACurveCrossesPointByPointAndAStrangerIsRefused()
    {
        string text = $$"""
            node = "{{Node}}"

            [[points]]
            counted = "bytes"
            observed_unix_nanos = 1000000000
            value = 1024

            [[points]]
            counted = "throughput"
            value = 1
            """;

        MeasurementRecord point = Assert.Single(Publications.Curve(text, out string none)!);

        Assert.Empty(none);
        Assert.Equal((Node, Counted.Bytes, 1024UL), (point.Scope, point.Counted, point.Value));
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(1), point.Observed);
        Assert.Null(Publications.Curve("points = 3", out string said));
        Assert.NotEmpty(said);
        Assert.Empty(Publications.Curve(string.Empty, out _)!);
    }
}
