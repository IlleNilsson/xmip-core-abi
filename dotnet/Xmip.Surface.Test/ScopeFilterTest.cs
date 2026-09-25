using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// A pattern applied to a publication: what a view shows, what it says, and
/// what it never hides. The three views of the web GUI narrow through this one
/// object, so a filter cannot mean one thing on the monitor and another in the
/// tree (ADR-0052 clause 1).
/// </summary>
public sealed class ScopeFilterTest
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly HealthRecord[] Leaves =
    [
        new("xmip:///C1/node/alpha/receive/tcp", HealthState.Fine, 0, "", Now),
        new("xmip:///C1/node/alpha/receive/file", HealthState.Stressed, 55, "slow", Now),
        new("xmip:///C1/node/beta/process/json", HealthState.Fine, 0, "", Now),
        new("xmip:///C1/node/gamma/send/tcp", HealthState.Done, 90, "refused", Now),
    ];

    private static ScopeIndex Index()
    {
        return ScopeIndex.Build(Leaves, [], 1, "a test wrote these");
    }

    [Fact]
    public void NoPatternShowsEverythingAndSaysNothing()
    {
        ScopeFilter filter = ScopeFilter.Over(Index(), "   ");

        Assert.False(filter.Filtering);
        Assert.Equal(string.Empty, filter.Said);
        Assert.All(Leaves, leaf => Assert.True(filter.Shows(leaf.Scope)));
        Assert.Equal(4, filter.Only(Leaves).Count);
    }

    [Fact]
    public void AMatchShowsThePathDownToItAndEverythingBeneathIt()
    {
        ScopeFilter filter = ScopeFilter.Over(Index(), "xmip:///C1/node/alpha");

        Assert.True(filter.IsMatch("xmip:///C1/node/alpha"));
        Assert.True(filter.Shows("xmip:///C1/node/alpha"));

        // On the way down: without these the match could not be reached.
        Assert.True(filter.Shows(ScopeTree.Root));
        Assert.True(filter.Shows("xmip:///C1"));
        Assert.True(filter.Shows("xmip:///C1/node"));

        // Beneath it: a node that matched shows what it holds.
        Assert.True(filter.Shows("xmip:///C1/node/alpha/receive/file"));

        // And nothing else.
        Assert.False(filter.Shows("xmip:///C1/node/gamma"));
        Assert.False(filter.Shows("xmip:///C1/node/gamma/send/tcp"));

        // A flat list asks a narrower question: what stands on the way to a
        // match is a row of the tree, and is not itself part of the answer.
        Assert.False(filter.Holds("xmip:///C1/node"));
        Assert.True(filter.Holds("xmip:///C1/node/alpha/receive/file"));
    }

    [Fact]
    public void AWildcardNarrowsToWhatItNames()
    {
        ScopeFilter filter = ScopeFilter.Over(Index(), "*/send/*");

        Assert.Equal(1, filter.Matched);
        Assert.Equal(
            ["xmip:///C1/node/gamma/send/tcp"],
            filter.Only(Leaves).Select(record => record.Scope));
        Assert.Contains("1 of ", filter.Said, StringComparison.Ordinal);
        Assert.Contains("*/send/*", filter.Said, StringComparison.Ordinal);
    }

    /// <summary>
    /// A pattern that names nothing says so, in the estate's words. An empty
    /// page reads as a healthy estate, and that is the failure this sentence
    /// exists to prevent.
    /// </summary>
    [Fact]
    public void APatternThatMatchesNothingIsRefusedInWords()
    {
        ScopeFilter filter = ScopeFilter.Over(Index(), "xmip:///C1/node/Q*");

        Assert.True(filter.Filtering);
        Assert.Equal(0, filter.Matched);
        Assert.StartsWith("REFUSED", filter.Said, StringComparison.Ordinal);
        Assert.Contains("xmip:///C1/node/Q*", filter.Said, StringComparison.Ordinal);
        Assert.Contains("scope(s) published", filter.Said, StringComparison.Ordinal);
        Assert.Empty(filter.Only(Leaves));
    }

    [Fact]
    public void TheBranchesAViewShowsAreTheOnesOnTheWayOrBeneath()
    {
        ScopeIndex index = Index();
        ScopeFilter filter = ScopeFilter.Over(index, "*/gamma");
        IReadOnlyList<Branch> nodes = filter.Only(index.Branches("xmip:///C1/node"));

        Assert.Equal(["gamma"], nodes.Select(branch => branch.Label));
        Assert.Equal(3, index.Branches("xmip:///C1/node").Count);
    }

    /// <summary>
    /// The topology filters its nodes, and a link is drawn only where both of
    /// its ends are still there: a line to something hidden points at nothing.
    /// </summary>
    [Fact]
    public void TheTopologyKeepsALinkOnlyWhereBothEndsAreShown()
    {
        TopologySnapshot snapshot = new(
            [
                Node("c", null, "C1", "xmip:///C1"),
                Node("r", "c", "alpha", "xmip:///C1/node/alpha"),
                Node("p", "c", "beta", "xmip:///C1/node/beta"),
            ],
            [Link("r-p", "r", "p")],
            Now,
            "a test wrote this");

        TopologySnapshot whole = ScopeFilter.None.Only(snapshot);
        Assert.Equal(3, whole.Nodes.Count);
        Assert.Single(whole.Links);

        TopologySnapshot narrowed = ScopeFilter.Over(Index(), "*/alpha").Only(snapshot);

        // The cluster stands: it is the way down to alpha.
        Assert.Equal(["C1", "alpha"], narrowed.Nodes.Select(node => node.Label));
        Assert.Empty(narrowed.Links);
    }

    private static TopologyNode Node(string id, string? parent, string label, string scope)
    {
        return new TopologyNode(
            id, parent, label, TopologyNodeKind.Node, scope,
            HealthState.Fine, TopologyOrigin.Both, 0D, 0D, string.Empty);
    }

    private static CommunicationLink Link(string id, string from, string to)
    {
        return new CommunicationLink(
            id, from, to, CommunicationPattern.SendReceive, TopologyOrigin.Observed,
            "handoff", HealthState.Fine, 1UL, 1D, 1D, 1D, 0U, string.Empty);
    }
}
