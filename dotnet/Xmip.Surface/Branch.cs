using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// One thing directly beneath a scope in the drill-down: its scope, the
/// segment it is called by, its mood — its own when it is a leaf, the rollup
/// when it is a parent (ADR-0041) — how many leaves it holds, and the worst
/// of them, whose evidence is what a Holding branch says beside the word.
/// </summary>
public sealed record Branch(
    string Scope,
    string Label,
    HealthState State,
    bool IsLeaf,
    int Count,
    HealthRecord Worst);
