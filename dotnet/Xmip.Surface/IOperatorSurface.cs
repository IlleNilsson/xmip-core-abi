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

    /// <summary>Pause everything at and beneath a scope, by <paramref name="who"/>.
    /// The first operation that acts rather than reads. Returns what the runtime
    /// said, for the operator to see.</summary>
    public string PauseScope(string scope, string who);

    /// <summary>Resume everything at and beneath a scope.</summary>
    public string ResumeScope(string scope);
}
