using Xmip.Abi.Operate;

namespace Xmip.Surface;

// What the topology says, as every surface writes it: what a kind, an
// origin, a pattern and a relation are called — each the runtime's word and
// name (observe::topology, xmip_operate.h section 8) — what passes over a
// link, and what a node is for (node::NodeRole::purpose, section 7).
public static partial class English
{
    /// <summary>What a person reads a topology kind as — <c>virtual
    /// machine</c> — <c>observe::NodeKind::name</c>, called in the runtime
    /// (ADR-0052, amendment 2026-09-25: the web GUI kept its own until then).
    /// <c>unknown</c> for a value the runtime does not define.</summary>
    public static string Name(TopologyNodeKind kind)
    {
        return RuntimeLibrary.Rules.Publications.Words(kind)?.Name ?? "unknown";
    }

    /// <summary>What a person reads an origin as — <c>configured and
    /// observed</c> — <c>observe::Origin::name</c>, called in the
    /// runtime.</summary>
    public static string Name(TopologyOrigin origin)
    {
        return RuntimeLibrary.Rules.Publications.Words(origin)?.Name ?? "unknown";
    }

    /// <summary>What a person reads a communication pattern as — <c>Publish →
    /// consume</c> — <c>observe::Pattern::name</c>, called in the
    /// runtime.</summary>
    public static string Name(CommunicationPattern pattern)
    {
        return RuntimeLibrary.Rules.Publications.Words(pattern)?.Name ?? "unknown";
    }

    /// <summary>What a person reads a relation as — <c>copies to</c> —
    /// <c>observe::Relation::name</c>, called in the runtime: how a link's two
    /// ends are related by their roles, never their names (ADR-0052, amendment
    /// 2026-11-13). <c>unknown</c> for a value the runtime does not
    /// define.</summary>
    public static string Name(TopologyRelation relation)
    {
        return RuntimeLibrary.Rules.Publications.Words(relation)?.Name ?? "unknown";
    }

    /// <summary>The word a publication writes a topology kind as —
    /// <c>virtual-machine</c> — <c>observe::NodeKind::word</c>, for a surface
    /// that styles by it. <c>unknown</c> for a value the runtime does not
    /// define.</summary>
    public static string Word(TopologyNodeKind kind)
    {
        return RuntimeLibrary.Rules.Publications.Words(kind)?.Word ?? "unknown";
    }

    /// <summary>The word a publication writes an origin as —
    /// <c>observe::Origin::word</c>.</summary>
    public static string Word(TopologyOrigin origin)
    {
        return RuntimeLibrary.Rules.Publications.Words(origin)?.Word ?? "unknown";
    }

    /// <summary>The word a publication writes a communication pattern as —
    /// <c>publish-consume</c> — <c>observe::Pattern::word</c>.</summary>
    public static string Word(CommunicationPattern pattern)
    {
        return RuntimeLibrary.Rules.Publications.Words(pattern)?.Word ?? "unknown";
    }

    /// <summary>The word a publication writes a relation as —
    /// <c>copies-to</c> — <c>observe::Relation::word</c>, for a surface that
    /// styles by it.</summary>
    public static string Word(TopologyRelation relation)
    {
        return RuntimeLibrary.Rules.Publications.Words(relation)?.Word ?? "unknown";
    }

    /// <summary>What passes over a communication link, as the topology says
    /// it on the line itself (ADR-0052, amendment 2026-09-25): the volume the
    /// publisher counted and the rate it stated, or — for a link configured
    /// and never used — that nothing has passed, so a path with no traffic is
    /// seen as such rather than as a zero. A link a relation explains says
    /// the relation first — <c>copies to · 48,213 · 310/s</c>, <c>writes into ·
    /// configured · no traffic observed</c> — and traffic no relation explains
    /// says the figure alone (ADR-0052, amendment 2026-11-13).</summary>
    public static string Traffic(CommunicationLink link)
    {
        ArgumentNullException.ThrowIfNull(link);

        return link.Relation == TopologyRelation.Traffic
            ? Passed(link)
            : $"{Name(link.Relation)} · {Passed(link)}";
    }

    /// <summary>What passed over a link without its relation — <c>48,213 ·
    /// 310/s</c>, or <c>configured · no traffic observed</c> — for a surface
    /// that says the relation apart, as the inspector's rows do.</summary>
    public static string Passed(CommunicationLink link)
    {
        ArgumentNullException.ThrowIfNull(link);

        return link.Origin == TopologyOrigin.Configured && link.Volume == 0
            ? "configured · no traffic observed"
            : $"{Figure(link.Volume)} · {Rate(link.Rate)}";
    }

    /// <summary>
    /// What a node declaring <paramref name="roles"/> is for — each role with
    /// its meaning, one per line, or <c>declares no role</c> —
    /// <c>node::NodeRole::purpose</c>, called in the runtime: what every
    /// surface shows of a node, on hover over it on the Topology and the
    /// Monitor, Web and Desktop alike, and beneath its row in <c>xmip</c> and
    /// PowerShell (the owner, 2026-11-12: <i>There has to be a tooltip,
    /// regardless of Web or MAUI. what are nodes purpose</i>; ADR-0056,
    /// amendment 2026-11-13). The roles are words separated by commas or
    /// <c>+</c>, a topology node's among them; a word that is no role is
    /// answered with its REFUSED sentence (ADR-0055).
    /// </summary>
    public static string Purpose(string roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        return RuntimeLibrary.Rules.Purpose(roles);
    }

    /// <summary>What a node declaring each of <paramref name="roles"/> is
    /// for: <see cref="Purpose(string)"/> over the words together.</summary>
    public static string Purpose(IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        return Purpose(string.Join(',', roles));
    }
}
