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
