using System.Text.Json;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeDesign"/> crosses section 10 of <c>xmip_operate.h</c>
/// to the runtime this estate built. The views, the edits and the slicing
/// are tested in Rust, where they are written (<c>xmip-core-configure</c>);
/// these prove the crossing: each answer whole and in the header's shape,
/// and a refusal carried as the runtime's sentence.
/// </summary>
public sealed class RuntimeDesignTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    private static string Text()
    {
        return $"[service]\nname = \"xmip\"\ncluster_name = \"{Cluster.Name}\"\n\n" +
            $"[nodes.{Cluster.Nodes[0]}]\n\n[nodes.{Cluster.Nodes[1]}]\n";
    }

    [Fact]
    public void TheViewsComeBackAsTheHeadersJson()
    {
        Assert.True(RuntimeRulesTest.Rules.Design.TryViews(Text(), out string answer), answer);

        using JsonDocument document = JsonDocument.Parse(answer);
        JsonElement views = document.RootElement.GetProperty("views");

        Assert.Equal(Cluster.Name, document.RootElement.GetProperty("cluster").GetString());
        Assert.Equal("cluster", document.RootElement.GetProperty("document").GetString());
        Assert.Equal("cluster", views[0].GetProperty("kind").GetString());
        Assert.Equal(2, views[1].GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public void AnEditComesBackAsTheEditedTextAndARefusalAsItsSentence()
    {
        const string edit = """{"set":{"section":["service"],"key":["name"],"value":"\"x\""}}""";

        Assert.True(RuntimeRulesTest.Rules.Design.TryEdit(Text(), edit, out string edited), edited);
        Assert.Contains("name = \"x\"", edited, StringComparison.Ordinal);

        Assert.False(RuntimeRulesTest.Rules.Design.TryEdit(Text(), "{}", out string refusal));
        Assert.NotEmpty(refusal);
    }

    [Fact]
    public void EachNodeIsSlicedAndANodesOwnDocumentIsRefused()
    {
        RuntimeDesign design = RuntimeRulesTest.Rules.Design;

        Assert.True(design.TrySlices(Text(), null, out string answer), answer);

        using JsonDocument document = JsonDocument.Parse(answer);
        JsonElement slices = document.RootElement.GetProperty("slices");

        Assert.Equal(2, slices.GetArrayLength());
        Assert.Contains(
            $"node_name = \"{slices[0].GetProperty("node").GetString()}\"",
            slices[0].GetProperty("text").GetString(),
            StringComparison.Ordinal);

        Assert.False(design.TrySlices(
            slices[0].GetProperty("text").GetString()!, null, out string refusal));
        Assert.Contains("[nodes]", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void AFilterCrossesToItsStructureAndBack()
    {
        const string filter = "exists Urgent";

        RuntimeDesign design = RuntimeRulesTest.Rules.Design;

        Assert.True(design.TryFilterStructure(filter, out string rows), rows);
        Assert.True(design.TryFilterText(rows, out string text), text);
        Assert.Equal(filter, text);
    }
}
