namespace Xmip.Abi.Operate;

/// <summary>
/// What a node declared of itself (ADR-0056) as the runtime read it: from
/// the capability record the node published
/// (<see cref="RuntimeRules.Published"/>, <c>observe::capability</c>) or
/// from a run's entry for it (<see cref="RuntimeRules.Entry"/>,
/// <c>node::Capability</c>).
/// </summary>
/// <param name="Node">The node's name, read as a name and nothing else.</param>
/// <param name="Stages">The stages it declared, in message-path order; none
/// when refused.</param>
/// <param name="Online">Whether it may assume the internet; a run's entry
/// never says, and a refused declaration says nothing.</param>
/// <param name="Refusal">Why the declaration was refused, in
/// <c>node::Stage::declared</c>'s words, or empty (ADR-0055).</param>
public sealed record DeclaredCapability(
    string Node, IReadOnlyList<string> Stages, bool Online, string Refusal);
