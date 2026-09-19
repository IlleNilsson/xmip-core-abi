namespace Xmip.Surface;

/// <summary>
/// What one node declares it can do (ADR-0056): the stages of the message
/// path it can serve, whether a route off its machine may be assumed, and the
/// evidence it published saying so. Nothing here is inferred from a node's
/// name — ADR-0056 clause 1, and the owner on 2026-09-19 when a rig read a
/// stage out of a first letter: <i>a name is not a criterion</i>.
/// </summary>
/// <remarks>
/// Two kinds of the four reach a surface today. A publisher that models
/// neither authentication nor runtime capability says so in its own evidence,
/// which is why <see cref="Evidence"/> is carried whole rather than reduced to
/// the stages: a surface repeats what the publisher said and invents no
/// silence (ADR-0014, amendment 2026-09-19).
/// </remarks>
public sealed record NodeCapability(
    string Node,
    IReadOnlyList<string> Stages,
    bool Online,
    bool Published,
    string Evidence)
{
    private const string Declares = "declares ";

    /// <summary>A node that declared nothing a surface can read.</summary>
    public static NodeCapability None { get; } = new(string.Empty, [], false, false, string.Empty);

    /// <summary>Whether there is a node behind this at all.</summary>
    public bool Said => Node.Length > 0;

    /// <summary>The stages as <c>--can</c> and <c>[run]</c> write them:
    /// <c>receive+process</c>, or the empty string when none is declared.</summary>
    public string Words => string.Join('+', Stages);

    /// <summary>The online capability in the word a health record carries.</summary>
    public string Route => Online ? "online" : "offline";

    /// <summary>Where the declaration came from, which is never the same
    /// question as what it says: a node publishes its capability, and
    /// <c>[run]</c> only says what the node was started with.</summary>
    public string Origin => Published
        ? "published by the node"
        : "what the run started it with";

    /// <summary>What every surface says of one node, in one phrase:
    /// <c>receive+process · online</c>. A node declaring no stage says so in
    /// words, because declaring none is a choice and not an absence.</summary>
    public string Line()
    {
        return Said
            ? $"{(Stages.Count > 0 ? Words : "no stage")} · {Route}"
            : string.Empty;
    }

    /// <summary>The capability a node published at
    /// <c>&lt;node&gt;/capability</c>, read from the evidence it wrote there.
    /// Evidence a surface does not recognise declares no stage and is still
    /// carried whole, so an operator reads what the node actually said.</summary>
    public static NodeCapability Declared(string node, string evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        string said = evidence.StartsWith(Declares, StringComparison.Ordinal)
            ? evidence[Declares.Length..]
            : string.Empty;
        int end = said.IndexOf(';', StringComparison.Ordinal);

        return new NodeCapability(
            node,
            Ordered(end < 0 ? said : said[..end]),
            evidence.Contains("; online;", StringComparison.Ordinal),
            true,
            evidence);
    }

    /// <summary>One entry of <c>[run].capabilities</c> — <c>R1=receive+send</c>,
    /// or a bare <c>n1</c> for a node started with no stage of its own. It says
    /// nothing of the online capability, which <c>[run]</c> lists apart.</summary>
    public static NodeCapability Started(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        int split = entry.IndexOf('=', StringComparison.Ordinal);

        return new NodeCapability(
            (split < 0 ? entry : entry[..split]).Trim(),
            split < 0 ? [] : Ordered(entry[(split + 1)..]),
            false,
            false,
            string.Empty);
    }

    /// <summary>The stage words a value names, in message-path order and each
    /// at most once; a word that is no stage is not one.</summary>
    private static IReadOnlyList<string> Ordered(string said)
    {
        string[] words = said.Split(
            [',', '+'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return [.. ScopeTree.Stages.Where(stage => words.Contains(stage, StringComparer.Ordinal))];
    }
}
