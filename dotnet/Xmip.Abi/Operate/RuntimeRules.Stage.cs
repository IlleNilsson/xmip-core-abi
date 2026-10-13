using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

// Section 7's stage, role and capability rules: what a stage counts, whether
// it pauses, what a thing at it is called, the roles a declaration names and
// the stages each serves, and a node's declared capability.
public sealed unsafe partial class RuntimeRules
{
    /// <summary>What a stage counts — <c>observe::Counted::at</c>: Streams at
    /// receive, Journeys in process, Messages at send. Null for a word that is
    /// no stage.</summary>
    public Counted? StageCounted(string stage)
    {
        using PinnedStr word = XmipStr.Pin(stage);
        int counted = 0;

        return _stageCounted(word.Value, &counted) == 0 ? (Counted)counted : null;
    }

    /// <summary>Whether an operator may pause a stage —
    /// <c>node::Stage::pausable</c>. Null for a word that is no stage.</summary>
    public bool? Pausable(string stage)
    {
        using PinnedStr word = XmipStr.Pin(stage);
        byte pausable = 0;

        return _stagePausable(word.Value, &pausable) == 0 ? pausable != 0 : null;
    }

    /// <summary>What a thing configured at a stage is called —
    /// <c>node::Stage::location</c>. Null for a word that is no stage.</summary>
    public string? Location(string stage)
    {
        using PinnedStr word = XmipStr.Pin(stage);
        XmipStr text;

        return _stageLocation(word.Value, &text) == 0 ? text.Read() : null;
    }

    /// <summary>
    /// The roles a declaration names, each once and in declaration order, the
    /// three stage roles together said as executing —
    /// <c>node::NodeRole::declared</c>. A declaration naming any other word
    /// declares no role, and <paramref name="refusal"/> is the REFUSED
    /// sentence naming it (ADR-0055); otherwise it is empty.
    /// </summary>
    public IReadOnlyList<string> Declared(string declared, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(declared);

        byte[] bytes = Encoding.UTF8.GetBytes(declared);
        XmipStr[] roles = new XmipStr[RoleWords.Count];
        byte[] said = [];
        nuint count = 0;
        nuint saidLength = Small;
        int status;

        // Asked again only when the refusal did not fit the first buffer.
        do
        {
            said = new byte[saidLength];

            fixed (byte* data = bytes)
            fixed (XmipStr* into = roles)
            fixed (byte* sentence = said)
            {
                status = _declared(
                    new XmipStr(data, (nuint)bytes.Length), into, (nuint)roles.Length, &count,
                    sentence, (nuint)said.Length, &saidLength);
            }
        }
        while (saidLength > (nuint)said.Length);

        if (status == (int)XmipStatus.Invalid)
        {
            refusal = Encoding.UTF8.GetString(said, 0, checked((int)saidLength));
            return [];
        }

        Check(status);
        refusal = string.Empty;

        // Static words: they outlive the call.
        return [.. roles.Take(checked((int)count)).Select(role => role.Read())];
    }

    /// <summary>
    /// The stages of the message path a role serves, in path order —
    /// <c>node::NodeRole::stages</c>: one for receiving, processing and
    /// sending, all three for executing, none for a role off the path. Empty
    /// for a word that is no role.
    /// </summary>
    public IReadOnlyList<string> RoleStages(string role)
    {
        ArgumentNullException.ThrowIfNull(role);

        using PinnedStr text = XmipStr.Pin(role);
        XmipStr* stages = stackalloc XmipStr[StageWords.Count];
        nuint count = 0;
        int status = _roleStages(text.Value, stages, (nuint)StageWords.Count, &count);

        if (status == (int)XmipStatus.NotFound)
        {
            return [];
        }

        Check(status);
        return Read(stages, count);
    }

    /// <summary>
    /// What the node a health record sits beneath declared, when the record is
    /// the capability record it publishes —
    /// <c>observe::capability::declared</c>. Null for any other record. A
    /// refused declaration carries its refusal and no role (ADR-0055).
    /// </summary>
    public DeclaredCapability? Published(string scope, string evidence)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(evidence);

        using PinnedStr scopeText = XmipStr.Pin(scope);
        using PinnedStr evidenceText = XmipStr.Pin(evidence);
        XmipStr* roles = stackalloc XmipStr[RoleWords.Count];
        byte* said = stackalloc byte[Small];
        XmipStr node;
        nuint count = 0;
        nuint saidLength = 0;
        byte online = 0;
        int status = _published(
            scopeText.Value, evidenceText.Value, &node, roles, (nuint)RoleWords.Count, &count,
            &online, said, Small, &saidLength);

        if (status == (int)XmipStatus.NotFound)
        {
            return null;
        }

        if (status == (int)XmipStatus.Invalid)
        {
            return new DeclaredCapability(node.Read(), [], false, Sentence(said, saidLength));
        }

        Check(status);
        return new DeclaredCapability(
            node.Read(), Read(roles, count), online != 0, string.Empty);
    }

    /// <summary>
    /// One entry of a run's node list — <c>&lt;node&gt;=receiving+sending</c>,
    /// or a bare name — as <c>node::Capability::from_entry</c> reads it. A
    /// refused entry keeps its name and carries its refusal (ADR-0055).
    /// </summary>
    public DeclaredCapability Entry(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        using PinnedStr text = XmipStr.Pin(entry);
        XmipStr* roles = stackalloc XmipStr[RoleWords.Count];
        byte* said = stackalloc byte[Small];
        XmipStr node;
        nuint count = 0;
        nuint saidLength = 0;
        int status = _entry(
            text.Value, &node, roles, (nuint)RoleWords.Count, &count, said, Small, &saidLength);

        if (status == (int)XmipStatus.Invalid)
        {
            return new DeclaredCapability(node.Read(), [], false, Sentence(said, saidLength));
        }

        Check(status);
        return new DeclaredCapability(node.Read(), Read(roles, count), false, string.Empty);
    }
}
