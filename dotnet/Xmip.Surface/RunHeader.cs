using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// What a run was started with, as its publisher says it under <c>[run]</c>:
/// the cluster, the tests, the nodes, the roles each of them declared,
/// which of them are online, and how hard. The owner, 2026-09-19: the run says
/// what it was started with — a board could not be told from the one before
/// it. A publication with no such table is <see cref="None"/>, and a surface
/// shows nothing for it. <see cref="Hidden"/> is what the run declared of
/// itself when it was started (ADR-0028, amendment 2026-09-30): an assistant's
/// test run, which a view leaves out until asked to show it.
/// </summary>
public sealed record RunHeader(
    string Cluster,
    IReadOnlyList<string> Tests,
    IReadOnlyList<string> Nodes,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Online,
    string Stress,
    bool Hidden = false)
{
    /// <summary>What a publication that says nothing of its run is read as.</summary>
    public static RunHeader None { get; } = new(string.Empty, [], [], [], [], string.Empty);

    /// <summary>Whether the publisher said anything.</summary>
    public bool Said =>
        Cluster.Length > 0 || Tests.Count > 0 || Nodes.Count > 0 || Stress.Length > 0;

    /// <summary>
    /// What one node was started with, by name — its roles in
    /// <c>[run].roles</c>, with the online capability <c>[run]</c>
    /// lists apart folded back in. <see cref="NodeCapability.None"/> when this
    /// run does not name the node. It is what the node was *started* with;
    /// what it *published* is <see cref="ScopeIndex.Capability"/>, and that is
    /// the one a surface prefers (ADR-0056 clause 1).
    /// </summary>
    public NodeCapability Capability(string node)
    {
        foreach (string entry in Roles)
        {
            NodeCapability started = NodeCapability.Started(entry);

            if (string.Equals(started.Node, node, StringComparison.Ordinal))
            {
                return started with { Online = Online.Contains(node, StringComparer.Ordinal) };
            }
        }

        return NodeCapability.None;
    }

    /// <summary>
    /// The one line every surface shows:
    /// <c>RoundTrip · &lt;cluster&gt; · nodes &lt;node&gt;=receiving
    /// &lt;other&gt;=processing+sending · online &lt;node&gt; · realistic</c>. A
    /// node is named with the roles it declared, because bare names say nothing a board could be told apart
    /// by; a publisher that says no roles leaves the names bare, as before.
    /// The names are the operator's and mean nothing to Xmip (ADR-0053). A part
    /// the publisher left out is left out; no nodes and none online are said in
    /// words, because both are choices.
    /// </summary>
    public string Line()
    {
        if (!Said)
        {
            return string.Empty;
        }

        List<string> parts = [];

        if (Tests.Count > 0)
        {
            parts.Add(string.Join(' ', Tests));
        }

        if (Cluster.Length > 0)
        {
            parts.Add(Cluster);
        }

        parts.Add(Nodes.Count > 0
            ? $"nodes {string.Join(' ', Nodes.Select(Declared))}"
            : "no nodes");

        if (Nodes.Count > 0)
        {
            parts.Add(Online.Count > 0 ? $"online {string.Join(' ', Online)}" : "none online");
        }

        if (Stress.Length > 0)
        {
            parts.Add(Stress);
        }

        if (Hidden)
        {
            parts.Add("hidden test run");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>The run a publication says it was started with, as the
    /// runtime read it (<c>observe::Run</c>), or <see cref="None"/> when it
    /// says nothing of its run.</summary>
    public static RunHeader From(PublishedRun? run)
    {
        return run is null
            ? None
            : new RunHeader(
                run.Cluster, run.Tests, run.Nodes, run.Roles, run.Online, run.Stress,
                run.Hidden);
    }

    /// <summary>A node as the run line names it: the publisher's own
    /// <c>[run].roles</c> entry for it, or the bare name when the publisher
    /// said nothing of its roles.</summary>
    private string Declared(string node)
    {
        foreach (string entry in Roles)
        {
            if (string.Equals(NodeCapability.Started(entry).Node, node, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return node;
    }

}
