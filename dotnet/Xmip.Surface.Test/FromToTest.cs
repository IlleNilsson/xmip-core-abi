using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// What the owner asked of every observation on 2026-11-10, read once in
/// <see cref="IOperatorSurface"/> for every view, command and cmdlet: from and
/// to on every row, as the runtime says them; the Work Process, never a bare
/// process, wherever a stage segment is read; and the Work Process card that
/// says none is placed while the process stage holds Subscriptions alone
/// (ADR-0052, amendment 2026-11-11).
/// </summary>
public sealed class FromToTest
{
    private static readonly TestCluster Test = TestCluster.Read();
    private static readonly string Cluster = Test.Scope;
    private static readonly string Processor = Test.WithRole("processing");
    private static readonly string Node = $"{Cluster}/node/{Processor}";

    // A node that receives at In, routes by onward to the Send Port Out, and
    // draws both, as a running node publishes itself.
    private static string Written()
    {
        string path = Path.Combine(Path.GetTempPath(), $"xmip-from-to-{Guid.NewGuid():N}.xmip");
        File.WriteAllText(path, $$"""
            node = "{{Cluster}}"

            [[records]]
            scope = "{{Node}}/receive/In"
            state = "fine"

            [[records]]
            scope = "{{Node}}/receive/In/delivery"
            state = "stressed"
            severity = 60
            evidence = "a document no Subscription takes was dropped"

            [[records]]
            scope = "{{Node}}/process/onward"
            state = "fine"

            [[subscriptions]]
            node = "{{Node}}"
            name = "onward"
            send_ports = ["Out"]

            [topology]
            observed_unix_nanos = 5

            [[topology.nodes]]
            id = "node/{{Processor}}/receive/In"
            kind = "endpoint"
            scope = "{{Node}}/receive/In"

            [[topology.links]]
            id = "in"
            from = "node/{{Processor}}/receive/In"
            to = "node/{{Processor}}/process/onward"
            """);
        return path;
    }

    [Fact]
    public void ARowAndWhatIsBeneathItSayFromAndToInTheRuntimesWords()
    {
        string path = Written();
        IOperatorSurface surface = new SnapshotOperator(path);

        FromTo location = surface.FromTo($"{Node}/receive/In");
        Assert.Equal($"In [{Processor}]", location.From);
        Assert.Equal("Out", location.To);
        Assert.Equal(location, surface.FromTo($"{Node}/receive/In/delivery"));
        Assert.Equal(FromTo.None, surface.FromTo(Cluster));

        ScopeItem row = surface.Describe($"{Node}/receive/In");
        Assert.Equal(($"In [{Processor}]", "Out"), (row.CameFrom, row.WentTo));
        Assert.Equal(
            ["Out"],
            Assert.Single(surface.Subscriptions().Subscriptions).Reaches);
        File.Delete(path);
    }

    [Fact]
    public void AStageSegmentReadsAsItsTitleOnEverySurfaceThatShowsIt()
    {
        string path = Written();
        IOperatorSurface surface = new SnapshotOperator(path);
        ScopeIndex index = surface.Index();

        Assert.Equal("Work Process", index.Label($"{Node}/process"));
        Assert.Equal("onward", index.Label($"{Node}/process/onward"));
        Assert.Equal(Processor, index.Label(Node));
        Assert.Equal(
            ["Work Process", "onward"],
            ScopeTree.Trail($"{Node}/process/onward", Cluster, index)
                .Skip(3)
                .Select(crumb => crumb.Label));
        File.Delete(path);
    }

    [Fact]
    public void TheWorkProcessCardSaysNoneIsPlacedWhileOnlySubscriptionsAreThere()
    {
        string path = Written();
        IOperatorSurface surface = new SnapshotOperator(path);

        Assert.True(surface.NoWorkProcessPlaced("process"));
        Assert.False(surface.NoWorkProcessPlaced("receive"), "a Location is no Subscription");

        File.AppendAllText(path, $$"""

            [[records]]
            scope = "{{Node}}/process/Sales order handling"
            state = "fine"
            """);
        IOperatorSurface again = new SnapshotOperator(path);
        Assert.False(
            again.NoWorkProcessPlaced("process"),
            "something beneath the stage that no Subscription is: a Work Process");
        File.Delete(path);
    }
}
