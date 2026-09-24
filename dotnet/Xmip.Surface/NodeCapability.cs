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

    /// <summary>What a node that declares no stage publishes in place of the
    /// words.</summary>
    private const string NoStage = "no stage of the message path";

    /// <summary>Why the declaration was refused, or the empty string when it
    /// was not: a word that is no stage refuses the whole declaration, in the
    /// words <c>node::Stage::declared</c> uses, and is never read as the
    /// words that were known (ADR-0055).</summary>
    public string Refusal { get; init; } = string.Empty;

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
    /// words, because declaring none is a choice and not an absence. A
    /// refused declaration says its refusal instead.</summary>
    public string Line()
    {
        return !Said
            ? string.Empty
            : Refusal.Length > 0
                ? Refusal
                : $"{(Stages.Count > 0 ? Words : "no stage")} · {Route}";
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
        string words = end < 0 ? said : said[..end];
        IReadOnlyList<string> stages = Ordered(
            string.Equals(words, NoStage, StringComparison.Ordinal) ? string.Empty : words,
            out string refusal);

        return new NodeCapability(
            node,
            stages,
            evidence.Contains("; online;", StringComparison.Ordinal),
            true,
            evidence)
        { Refusal = refusal };
    }

    /// <summary>One entry of <c>[run].capabilities</c> —
    /// <c>edge-01=receive+send</c>, or a bare <c>edge-02</c> for a node started
    /// with no stage of its own. Whatever the node is called is read as a name
    /// and nothing else. It says nothing of the online capability, which
    /// <c>[run]</c> lists apart.</summary>
    public static NodeCapability Started(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        int split = entry.IndexOf('=', StringComparison.Ordinal);
        IReadOnlyList<string> stages = Ordered(
            split < 0 ? string.Empty : entry[(split + 1)..], out string refusal);

        return new NodeCapability(
            (split < 0 ? entry : entry[..split]).Trim(),
            stages,
            false,
            false,
            string.Empty)
        { Refusal = refusal };
    }

    /// <summary>
    /// The stage words a value names, in message-path order and each at most
    /// once, by the rule <c>node::Stage::declared</c> holds in Rust: words
    /// separated by commas or <c>+</c>, each exact lowercase only (the owner,
    /// 2026-09-24: <c>Send</c> is no stage). Any other word refuses the whole
    /// value: no stages, and <paramref name="refusal"/> names every such word.
    /// </summary>
    /// <remarks>
    /// A copy of the Rust parse, and the one that remains: a surface reads a
    /// snapshot with no runtime library loaded, and the operator boundary
    /// (<c>xmip_operate.h</c>) carries no call for it. Estate test
    /// <c>test/XmipTest.Test.ps1</c> holds the words and the refusal equal.
    /// </remarks>
    private static IReadOnlyList<string> Ordered(string said, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(said);

        string[] words = said.Split(
            [',', '+'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string[] strangers = [.. words.Where(
            word => !ScopeTree.Stages.Contains(word, StringComparer.Ordinal))];

        if (strangers.Length > 0)
        {
            refusal = $"REFUSED: no capability is called {string.Join(", ", strangers)}; " +
                $"a node declares {string.Join(", ", ScopeTree.Stages)}, or nothing at all.";
            return [];
        }

        refusal = string.Empty;
        return [.. ScopeTree.Stages.Where(
            stage => words.Contains(stage, StringComparer.Ordinal))];
    }
}
