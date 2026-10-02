using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// What one node declares (ADR-0056): its roles, whether a route off its
/// machine may be assumed, and the evidence it published saying so. Its
/// roles are the one declaration of what it is for and which stages of the
/// message path it serves — receiving, processing, sending, executing (all
/// three in one process), operational, monitoring, development, storage
/// (ADR-0056, amendments 2026-10-01). Nothing here is inferred from a node's name —
/// ADR-0056 clause 1, and the owner on 2026-09-19 when a rig read a stage out
/// of a first letter: <i>a name is not a criterion</i>.
/// </summary>
/// <remarks>
/// Two kinds of the four reach a surface today. A publisher that models
/// neither authentication nor runtime capability says so in its own evidence,
/// which is why <see cref="Evidence"/> is carried whole rather than reduced to
/// the roles: a surface repeats what the publisher said and invents no
/// silence (ADR-0014, amendment 2026-09-19).
/// </remarks>
public sealed record NodeCapability(
    string Node,
    IReadOnlyList<string> Roles,
    bool Online,
    bool Published,
    string Evidence)
{
    /// <summary>Why the declaration was refused, or the empty string when it
    /// was not: a word that is no role refuses the whole declaration, in
    /// <c>node::NodeRole::declared</c>'s own words, and is never read as the
    /// words that were known (ADR-0055).</summary>
    public string Refusal { get; init; } = string.Empty;

    /// <summary>A node that declared nothing a surface can read.</summary>
    public static NodeCapability None { get; } = new(string.Empty, [], false, false, string.Empty);

    /// <summary>Whether there is a node behind this at all.</summary>
    public bool Said => Node.Length > 0;

    /// <summary>The roles as <c>--role</c> and <c>[run]</c> write them:
    /// <c>receiving+processing</c>, or the empty string when none is
    /// declared.</summary>
    public string Words => string.Join('+', Roles);

    /// <summary>The stages of the message path the node's roles serve, in path
    /// order: <c>node::NodeRole::stages</c> for each role, called in the
    /// runtime.</summary>
    public IReadOnlyList<string> Stages =>
    [
        .. ScopeTree.Stages.Where(stage => Roles.Any(
            role => RuntimeLibrary.Rules.RoleStages(role).Contains(stage, StringComparer.Ordinal))),
    ];

    /// <summary>The online capability in the word a health record carries.</summary>
    public string Route => Online ? "online" : "offline";

    /// <summary>Where the declaration came from, which is never the same
    /// question as what it says: a node publishes its capability, and
    /// <c>[run]</c> only says what the node was started with.</summary>
    public string Origin => Published
        ? "published by the node"
        : "what the run started it with";

    /// <summary>What every surface says of one node, in one phrase:
    /// <c>receiving+processing · online</c>. A node declaring no role says so
    /// in words, because declaring none is a choice and not an absence. A
    /// refused declaration says its refusal instead.</summary>
    public string Line()
    {
        return !Said
            ? string.Empty
            : Refusal.Length > 0
                ? Refusal
                : $"{(Roles.Count > 0 ? Words : "no role")} · {Route}";
    }

    /// <summary>What a node published of itself, when the record at
    /// <paramref name="scope"/> is the capability record it publishes at
    /// <c>&lt;node&gt;/capability</c>: the node beneath which it sits, the
    /// roles and online capability its evidence declares, and the evidence
    /// whole, so an operator reads what the node actually said. Null for any
    /// other record. Where the record sits and how its evidence reads are
    /// <c>observe::capability</c>'s and <c>node::Capability</c>'s, called in
    /// the runtime; nothing here reads a leaf's name or the evidence's
    /// words.</summary>
    public static NodeCapability? Declared(string scope, string evidence)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(evidence);

        return RuntimeLibrary.Rules.Published(scope, evidence) is { } said
            ? new NodeCapability(said.Node, said.Roles, said.Online, true, evidence)
            {
                Refusal = said.Refusal,
            }
            : null;
    }

    /// <summary>One entry of <c>[run].roles</c> —
    /// <c>edge-01=receiving+sending</c>, or a bare <c>edge-02</c> for a node
    /// started with no role of its own — as <c>node::Capability::from_entry</c>
    /// reads it in the runtime. Whatever the node is called is read as a name
    /// and nothing else. It says nothing of the online capability, which
    /// <c>[run]</c> lists apart.</summary>
    public static NodeCapability Started(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        DeclaredCapability said = RuntimeLibrary.Rules.Entry(entry);

        return new NodeCapability(said.Node, said.Roles, false, false, string.Empty)
        {
            Refusal = said.Refusal,
        };
    }

    /// <summary>
    /// The roles a value names, each at most once and in declaration order,
    /// receiving, processing and sending together said as executing, their
    /// sum: <c>node::NodeRole::declared</c>, called in the runtime — words
    /// separated by commas or <c>+</c>, each exact lowercase only (the owner,
    /// 2026-09-24). Any other word refuses the whole value: no roles, and
    /// <paramref name="refusal"/> is the REFUSED sentence naming every such
    /// word (ADR-0055). The one parse every surface and the estate's
    /// PowerShell module read a declaration by.
    /// </summary>
    public static IReadOnlyList<string> Ordered(string said, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(said);

        return RuntimeLibrary.Rules.Declared(said, out refusal);
    }

    /// <summary>The words a node may declare, its roles:
    /// <c>node::NodeRole::WORDS</c>, called in the runtime.</summary>
    public static IReadOnlyList<string> RoleWords => RuntimeLibrary.Rules.RoleWords;
}
