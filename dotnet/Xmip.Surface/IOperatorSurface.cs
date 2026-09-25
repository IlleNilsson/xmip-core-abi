using System.Runtime.CompilerServices;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// What every screen, command and cmdlet reads. Two implementations:
/// <see cref="NativeOperator"/> crosses the C ABI in <c>xmip_operate.h</c>
/// through the binding in xmip-core-abi, and <see cref="SnapshotOperator"/>
/// reads a snapshot a node published. The records themselves —
/// <see cref="HealthRecord"/>, <see cref="MeasurementRecord"/>,
/// <see cref="HealthState"/>, <see cref="Counted"/> — are the binding's, so a
/// screen and a cmdlet read the same shape.
/// </summary>
/// <remarks>
/// ADR-0027: a surface reads snapshots the runtime published and never asks
/// the hot path. Nothing here can make a node wait. ADR-0052: there is no
/// surface that invents records; a surface that cannot reach anything says
/// so in <see cref="Source"/> and reports nothing.
/// </remarks>
public interface IOperatorSurface
{
    /// <summary>Where the records come from, shown on screen so nobody
    /// mistakes a stale file for a node.</summary>
    public string Source { get; }

    /// <summary>Health at and beneath a scope, worst first, most severe first
    /// within a mood.</summary>
    public IReadOnlyList<HealthRecord> Health(string scope);

    /// <summary>One kind of count, summed over the scope.</summary>
    public MeasurementRecord? Measure(string scope, Counted counted);

    /// <summary>The six figures at a scope, in the order every surface says them.</summary>
    public Figures Figures(string scope)
    {
        return Surface.Figures.Read(this, scope);
    }

    /// <summary>
    /// The publication as the scope tree it is, built once and answered from
    /// by lookup (ADR-0052, amendment 2026-09-15: the index). A surface that
    /// keeps one per publication returns it; this default builds one from
    /// everything beneath the root.
    /// </summary>
    public ScopeIndex Index()
    {
        return ScopeIndex.Build(Health(ScopeTree.Root), [], 0, Source);
    }

    /// <summary>One scope as a row of the tree.</summary>
    public ScopeItem Describe(string scope)
    {
        return ScopeItem.Read(this, scope);
    }

    /// <summary>The direct children beneath a scope, one row each.</summary>
    public IReadOnlyList<ScopeItem> Children(string scope)
    {
        return ScopeItem.Children(this, scope);
    }

    /// <summary>
    /// The configured and observed communication topology. A surface that
    /// cannot publish topology returns an empty snapshot and names why in its
    /// source; it never infers application meaning from network traffic.
    /// </summary>
    public TopologySnapshot Topology()
    {
        return TopologySnapshot.Empty($"{Source} — topology is not published by this surface");
    }

    /// <summary>
    /// The scopes of the nodes this surface reads, in ordinal order: each thing
    /// the publisher's topology draws as a node (<c>observe::topology</c>'s node
    /// kind), where a topology is published; with none to say, each scope
    /// directly beneath the root, which is where a node's own publication puts
    /// itself. Until 2026-09-25 the Monitor counted the scopes beneath the root
    /// alone, and a Playground cluster — its nodes beneath
    /// <c>xmip:///&lt;cluster&gt;/node</c> — came out as one node, the cluster
    /// (ADR-0052, amendment 2026-09-25).
    /// </summary>
    public IReadOnlyList<string> NodeScopes()
    {
        string[] drawn =
        [
            .. Topology().Nodes
                .Where(node => node.Kind == TopologyNodeKind.Node)
                .Select(node => node.Scope)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        return drawn.Length > 0 ? drawn : [.. Index().Nodes().Select(node => ScopeTree.Root + node)];
    }

    /// <summary>
    /// What the run behind this surface was started with — the tests, the
    /// nodes, which of them are online, how hard — when its publisher says;
    /// <see cref="RunHeader.None"/> when it does not, which every surface
    /// but a Playground snapshot answers today.
    /// </summary>
    public RunHeader Run()
    {
        return RunHeader.None;
    }

    /// <summary>
    /// What one node declares it can do (ADR-0056), by name. What the node
    /// itself published wins; a node the publication holds no capability
    /// record for falls back to what <c>[run]</c> says it was started with,
    /// and <see cref="NodeCapability.Published"/> says which of the two a
    /// surface is showing. A face that already holds an index asks the index;
    /// none of them parses the published file itself (ADR-0014, amendment
    /// 2026-09-19).
    /// </summary>
    public NodeCapability Capability(string node)
    {
        NodeCapability published = Index().Capability(node);

        return published.Said ? published : Run().Capability(node);
    }

    /// <summary>
    /// Changes to the snapshots this surface reads. The first item announces
    /// the current view; later items arrive when its publisher advances.
    /// Implementations may coalesce changes because snapshots, not events, are
    /// the source of truth.
    /// </summary>
    public async IAsyncEnumerable<SurfaceChange> WatchAsync(
        [EnumeratorCancellation] CancellationToken stop = default)
    {
        yield return SurfaceChange.Initial(Source);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stop).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the normal end of a watch.
        }
    }

    /// <summary>Pause everything at and beneath a scope, by <paramref name="who"/>.
    /// The first operation that acts rather than reads. Returns what the runtime
    /// said, for the operator to see.</summary>
    public string PauseScope(string scope, string who);

    /// <summary>Resume everything at and beneath a scope.</summary>
    public string ResumeScope(string scope);

    /// <summary>
    /// Apply one <see cref="ScopeAction"/> and say what came of it. The
    /// default takes the surface at its word; a surface that knows whether
    /// the runtime applied the act overrides this and says so.
    /// </summary>
    public ScopeOperation Control(string scope, ScopeAction action, string who)
    {
        string said = action == ScopeAction.Pause
            ? PauseScope(scope, who)
            : ResumeScope(scope);

        return new ScopeOperation(scope, action, true, said);
    }
}
