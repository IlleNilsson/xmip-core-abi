using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// What an Operator does to a Message that failed (runtime-model.md section
/// 13; ADR-0013): <c>observe::Act</c> as <c>observe::Noun::Message</c> takes
/// it, whose word — the name, lower case — is what crosses to the runtime.
/// </summary>
public enum MessageAct
{
    /// <summary>Write it Active, its tries begun anew, and send it again from
    /// the end of its Send Port's queue — or from its place, where it blocks
    /// its sequence.</summary>
    Retry,

    /// <summary>Write it Dismissed — terminal, its history, Message and
    /// Stream kept — and take it out of its Send Port's queue.</summary>
    Dismiss,
}

/// <summary>
/// What came of a <see cref="MessageAct"/> on the Message whose Journey is
/// <paramref name="Journey"/> sent by the node at <paramref name="Node"/>:
/// whether it was applied — or, through a publication, left for the node —
/// and what was said. The executable and the PowerShell module emit this one
/// shape, as they emit a <see cref="DeadMessageOperation"/>.
/// </summary>
public sealed record MessageOperation(
    string Node, string Journey, MessageAct Act, bool Applied, string Result)
{
    /// <summary>The noun an order on a failed Message is left under:
    /// <c>observe::Noun::Message</c>'s word.</summary>
    public const string Noun = "message";

    /// <summary>The word the runtime names <paramref name="act"/> by.</summary>
    public static string Word(MessageAct act)
    {
        return act.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// The node and the Send Port a scope names: the node it is on
    /// (<see cref="ScopeTree.NodeScope"/>) and the segment after its stage,
    /// where it is that deep — empty for each it does not name.
    /// </summary>
    public static (string Node, string Port) PortAt(string scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        string node = ScopeTree.NodeScope(scope);

        if (node.Length == 0 || ScopeTree.Stage(scope).Length == 0)
        {
            return (node, string.Empty);
        }

        int depth = ScopeTree.Parts(node).Length;

        return (node, ScopeTree.Segment(scope, depth + 1));
    }

    /// <summary>
    /// What of <paramref name="failed"/> lies at or beneath
    /// <paramref name="scope"/>: every Port of its node, or the one Port it
    /// names, or every Port of the nodes beneath a scope on no node.
    /// </summary>
    public static FailedMessageList Within(FailedMessageList failed, string scope)
    {
        ArgumentNullException.ThrowIfNull(failed);
        ArgumentNullException.ThrowIfNull(scope);

        (string node, string port) = PortAt(scope);

        return failed with
        {
            Ports =
            [
                .. failed.Ports.Where(at =>
                    (node.Length == 0
                        ? ScopeTree.Beneath(at.Node, scope)
                        : string.Equals(at.Node, node, StringComparison.Ordinal))
                    && (port.Length == 0
                        || string.Equals(at.SendPort, port, StringComparison.Ordinal))),
            ],
        };
    }

    /// <summary>
    /// The Messages that failed at <paramref name="scope"/>, as
    /// <paramref name="read"/> lists them — or, where Xmip Storage did not
    /// answer it, <see cref="FailedMessageList.Unanswered"/> in the runtime's
    /// words: said, not thrown, so a screen or a command shows it and goes on.
    /// </summary>
    public static FailedMessageList Read(string scope, Func<FailedMessageList> read)
    {
        ArgumentNullException.ThrowIfNull(read);

        try
        {
            return read();
        }
        catch (InvalidOperationException unanswered)
        {
            return FailedMessageList.Unanswered(
                $"the Messages that failed at {scope} could not be read: {unanswered.Message}");
        }
    }

    /// <summary>
    /// What a list of the Messages that failed at <paramref name="scope"/>
    /// says where it is no answer, read from <paramref name="source"/>: what
    /// the surface was told where it asked (<c>FAILED:</c>), or that it
    /// cannot list them, so nothing is known of the queue (<c>NOT
    /// LISTED:</c>). Empty where the list is an answer. The one sentence
    /// every surface says it with.
    /// </summary>
    public static string Unlisted(FailedMessageList failed, string scope, string source)
    {
        ArgumentNullException.ThrowIfNull(failed);

        return failed.Failure.Length > 0 ? failed.Failure
            : failed.Listed ? string.Empty
            : $"NOT LISTED: {source} cannot list the Messages that failed, so whether any "
                + $"wait at or beneath {scope} is not known — not that none do.";
    }

    /// <summary>The act not taken, and why.</summary>
    public static MessageOperation Declined(
        string node, string journey, MessageAct act, string why)
    {
        return new MessageOperation(node, journey, act, false, why);
    }

    /// <summary>
    /// The last Message that failed at a Send Port, read from the evidence
    /// its node publishes at the Port's scope — the clause
    /// <c>; the Message of the Journey &lt;id&gt; failed: &lt;why&gt;</c> the runtime's
    /// <c>send_step::PortFigures::evidence</c> writes — or null where the
    /// evidence names none. The one reader of that clause; every surface
    /// that offers an act on the Message asks it.
    /// </summary>
    public static string? FailedIn(string? evidence)
    {
        const string Opening = "; the Message of the Journey ";
        const string Closing = " failed: ";

        int start = evidence?.IndexOf(Opening, StringComparison.Ordinal) ?? -1;

        if (evidence is null || start < 0)
        {
            return null;
        }

        start += Opening.Length;
        int end = evidence.IndexOf(Closing, start, StringComparison.Ordinal);

        return end > start && !evidence.AsSpan(start, end - start).ContainsAny(" ;")
            ? evidence[start..end]
            : null;
    }

    /// <summary>
    /// How many Messages that failed wait in a Send Port's queue now, read
    /// from the evidence its node publishes at the Port's scope — the figure
    /// <c>failed in its queue &lt;n&gt;</c> the runtime's
    /// <c>send_step::PortFigures::evidence</c> writes — or null where the
    /// evidence says none. Unlike <see cref="FailedIn"/>, which names the
    /// last Message that ever failed there, this is the queue as it stands:
    /// zero once every failed Message was retried or dismissed.
    /// </summary>
    public static ulong? FailingIn(string? evidence)
    {
        const string Opening = "failed in its queue ";

        int start = evidence?.IndexOf(Opening, StringComparison.Ordinal) ?? -1;

        if (evidence is null || start < 0)
        {
            return null;
        }

        start += Opening.Length;
        int end = start;

        while (end < evidence.Length && char.IsAsciiDigit(evidence[end]))
        {
            end++;
        }

        return ulong.TryParse(
            evidence.AsSpan(start, end - start),
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out ulong failing)
            ? failing
            : null;
    }

    /// <summary>
    /// Take <paramref name="act"/> on <paramref name="journey"/> through
    /// <paramref name="acting"/> with the scope of the node
    /// <paramref name="scope"/> is on — the node itself, or a Send Port's
    /// scope beneath it, as an operator sees the Message there
    /// (<see cref="ScopeTree.NodeScope"/>); declined in words, before any
    /// node is asked, where it is on no node or no Journey is named.
    /// </summary>
    public static MessageOperation OnNode(
        string scope, string journey, MessageAct act, Func<string, MessageOperation> acting)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(journey);
        ArgumentNullException.ThrowIfNull(acting);

        if (journey.Length == 0)
        {
            return Declined(
                scope, journey, act, $"REFUSED: to {Word(act)} a Message, name its Journey");
        }

        string node = ScopeTree.NodeScope(scope);

        return node.Length > 0
            ? acting(node)
            : Declined(
                scope, journey, act,
                $"REFUSED: {scope} is on no node; name the node that sends the Message's "
                + "Send Port, or the Send Port's scope beneath it");
    }
}
