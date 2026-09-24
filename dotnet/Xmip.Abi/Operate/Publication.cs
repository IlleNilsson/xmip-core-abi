namespace Xmip.Abi.Operate;

/// <summary>
/// A publication — a node's or a roll's snapshot file — as the runtime read
/// it (<c>xmip_operate.h</c> section 8). The shape is
/// <c>observe::Publication</c>'s and nobody else's: a surface hands the text
/// to <see cref="PublicationReader"/> and gets this back, and writes no key,
/// word or fallback of its own (ADR-0052, amendment 2026-09-24).
/// </summary>
/// <param name="Source">Who published, in the publisher's words.</param>
/// <param name="Node">The scope it publishes at.</param>
/// <param name="Records">Its health records, worst first.</param>
/// <param name="Counts">Its counts, each at the scope it was recorded at.</param>
/// <param name="Topology">The topology it draws, or null.</param>
/// <param name="Run">What its run was started with, or null.</param>
public sealed record Publication(
    string Source,
    string Node,
    IReadOnlyList<HealthRecord> Records,
    IReadOnlyList<MeasurementRecord> Counts,
    TopologySnapshot? Topology,
    PublishedRun? Run);

/// <summary>What a run was started with, as a publication says it under
/// <c>[run]</c>: <c>observe::Run</c>.</summary>
/// <param name="Cluster">The cluster's name.</param>
/// <param name="Tests">The tests that run.</param>
/// <param name="Nodes">The nodes spawned.</param>
/// <param name="Capabilities">What each node was started with, as
/// <c>node::Capability::entry</c> writes it.</param>
/// <param name="Online">The nodes that may assume the internet.</param>
/// <param name="Stress">The stress level's name.</param>
public sealed record PublishedRun(
    string Cluster,
    IReadOnlyList<string> Tests,
    IReadOnlyList<string> Nodes,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Online,
    string Stress);
