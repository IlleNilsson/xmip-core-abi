using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 7 of <c>include/xmip_operate.h</c>, crossed by P/Invoke: the rules
/// a surface calls instead of keeping its own. Scope containment, a scope's
/// parts, and the node and stage it is on are <c>observe::Scope</c>'s, and
/// the one wildcard over scopes <c>observe::wildcard</c>'s; the
/// stage words, whether a stage pauses and what a thing at it is called are
/// <c>node::Stage</c>'s, the role words and their parse
/// <c>node::NodeRole</c>'s, and a run's node entry <c>node::Capability</c>'s; a
/// mood's word, its color name, the rollup and the worst-first order are
/// <c>observe::Health</c>'s and <c>observe::Standing</c>'s; a counted kind's
/// word and what a stage counts are <c>observe::Counted</c>'s, and a
/// published capability record <c>observe::capability</c>'s. The runtime's
/// library forwards each, and this binds each once for every .NET surface
/// (ADR-0052 and ADR-0027, amendments 2026-09-24). Section 8, a publication
/// read by the runtime, is <see cref="Publications"/>; section 9, a
/// program's audit record, is <see cref="Audit"/>; section 10, the
/// cluster's <c>xmip.toml</c> read, edited and sliced, is
/// <see cref="Design"/>; section 11, Events
/// subscribed, is <see cref="Events"/>; section 12, the technologies the
/// runtime carries, is <see cref="Catalogue"/>; section 13, a System
/// Process declared, is <see cref="Processes"/>; section 20, a database's
/// schema upgraded, is <see cref="Databases"/>.
/// </summary>
/// <remarks>
/// Pure calls: none reads the snapshot or holds a table, so one instance
/// serves a whole process from any thread and is never released. What it
/// answers is copied out at once; nothing borrowed outlives the call. A
/// library that is the runtime's remote build, the phone's, carries sections
/// 7, 8, 9, 17's client, 18 and 19's request only: each node's binding here,
/// sections 10 to 16 and 19's signing, then throws an
/// <see cref="InvalidOperationException"/> saying so (<see cref="RemoteBuild"/>).
/// </remarks>
// Every call encodes its strings into stack buffers the encoder then fills;
// zeroing them first is wasted on a path a board takes thousands of times.
[SkipLocalsInit]
public sealed unsafe partial class RuntimeRules
{
    // A scope or a word is short. Up to this many bytes it is encoded on the
    // stack, because a board compares thousands of records at a time.
    private const int Small = 512;

    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, int> _contains;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, int> _matches;
    private readonly delegate* unmanaged[Cdecl]<byte, byte, byte*, int> _shown;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr*, nuint, nuint*, int> _parts;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr*, XmipStr*, int> _node;
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr*, nuint, nuint*, byte*, nuint, nuint*, int> _declared;
    private readonly delegate* unmanaged[Cdecl]<int, XmipStr*, int> _word;
    private readonly delegate* unmanaged[Cdecl]<int, XmipStr*, int> _color;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, int*, int> _named;
    private readonly delegate* unmanaged[Cdecl]<XmipHealthEntry*, nuint, nuint*, int> _order;
    private readonly delegate* unmanaged[Cdecl]<int, int*, int> _rolled;
    private readonly delegate* unmanaged[Cdecl]<int, XmipStr*, int> _countedWord;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, int*, int> _stageCounted;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, byte*, int> _stagePausable;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr*, int> _stageLocation;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr*, nuint, nuint*, int> _roleStages;
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, XmipStr*, XmipStr*, nuint, nuint*, byte*, byte*, nuint, nuint*, int>
        _published;
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr*, XmipStr*, nuint, nuint*, byte*, nuint, nuint*, int> _entry;

    // Sections 10 to 16: a node's, null in a remote build (RemoteBuild).
    private readonly nint _library;
    private readonly RuntimeDesign? _design;
    private readonly RuntimeEvents? _events;
    private readonly RuntimeCatalogue? _catalogue;
    private readonly RuntimeProcesses? _processes;
    private readonly RuntimeEventSubscriptions? _eventSubscriptions;
    private readonly RuntimeSubscriptions? _subscriptions;
    private readonly RuntimeDeadMessages? _deadMessages;
    private readonly RuntimeJourneys? _journeys;
    private readonly RuntimeDatabases? _databases;
    private readonly RuntimeSecureSurface? _hosting;

    private RuntimeRules(nint library, string path)
    {
        Source = path;
        _contains = (delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, int>)
            Export(library, OperateAbi.ScopeContainsEntrypoint);
        _matches = (delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, int>)
            Export(library, OperateAbi.ScopeMatchesEntrypoint);
        _shown = (delegate* unmanaged[Cdecl]<byte, byte, byte*, int>)
            Export(library, OperateAbi.RunShownEntrypoint);
        _parts = (delegate* unmanaged[Cdecl]<XmipStr, XmipStr*, nuint, nuint*, int>)
            Export(library, OperateAbi.ScopePartsEntrypoint);
        _node = (delegate* unmanaged[Cdecl]<XmipStr, XmipStr*, XmipStr*, int>)
            Export(library, OperateAbi.ScopeNodeEntrypoint);
        _declared = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr*, nuint, nuint*, byte*, nuint, nuint*, int>)
            Export(library, OperateAbi.RoleDeclaredEntrypoint);
        _word = (delegate* unmanaged[Cdecl]<int, XmipStr*, int>)
            Export(library, OperateAbi.HealthWordEntrypoint);
        _color = (delegate* unmanaged[Cdecl]<int, XmipStr*, int>)
            Export(library, OperateAbi.HealthColorEntrypoint);
        _named = (delegate* unmanaged[Cdecl]<XmipStr, int*, int>)
            Export(library, OperateAbi.HealthNamedEntrypoint);
        _order = (delegate* unmanaged[Cdecl]<XmipHealthEntry*, nuint, nuint*, int>)
            Export(library, OperateAbi.HealthOrderEntrypoint);
        _rolled = (delegate* unmanaged[Cdecl]<int, int*, int>)
            Export(library, OperateAbi.HealthRolledEntrypoint);
        _countedWord = (delegate* unmanaged[Cdecl]<int, XmipStr*, int>)
            Export(library, OperateAbi.CountedWordEntrypoint);
        _stageCounted = (delegate* unmanaged[Cdecl]<XmipStr, int*, int>)
            Export(library, OperateAbi.StageCountedEntrypoint);
        _stagePausable = (delegate* unmanaged[Cdecl]<XmipStr, byte*, int>)
            Export(library, OperateAbi.StagePausableEntrypoint);
        _stageLocation = (delegate* unmanaged[Cdecl]<XmipStr, XmipStr*, int>)
            Export(library, OperateAbi.StageLocationEntrypoint);
        _roleStages = (delegate* unmanaged[Cdecl]<XmipStr, XmipStr*, nuint, nuint*, int>)
            Export(library, OperateAbi.RoleStagesEntrypoint);
        _published = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr, XmipStr*, XmipStr*, nuint, nuint*, byte*, byte*, nuint, nuint*, int>)
            Export(library, OperateAbi.CapabilityPublishedEntrypoint);
        _entry = (delegate* unmanaged[Cdecl]<
            XmipStr, XmipStr*, XmipStr*, nuint, nuint*, byte*, nuint, nuint*, int>)
            Export(library, OperateAbi.CapabilityEntryEntrypoint);
        Publications = new PublicationReader(library);
        _library = library;
        Audit = new RuntimeAudit(library);
        _design = Node(library, RuntimeDesign.Entrypoints, () => new RuntimeDesign(library));
        _events = Node(library, RuntimeEvents.Entrypoints, () => new RuntimeEvents(library));
        _catalogue = Node(
            library, RuntimeCatalogue.Entrypoints, () => new RuntimeCatalogue(library));
        _processes = Node(
            library, RuntimeProcesses.Entrypoints, () => new RuntimeProcesses(library));
        _eventSubscriptions = Node(
            library,
            RuntimeEventSubscriptions.Entrypoints,
            () => new RuntimeEventSubscriptions(library));
        _subscriptions = Node(
            library, RuntimeSubscriptions.Entrypoints, () => new RuntimeSubscriptions(library));
        _deadMessages = Node(
            library, RuntimeDeadMessages.Entrypoints, () => new RuntimeDeadMessages(library));
        _journeys = Node(
            library, RuntimeJourneys.Entrypoints, () => new RuntimeJourneys(library));
        _databases = Node(
            library, RuntimeDatabases.Entrypoints, () => new RuntimeDatabases(library));
        _hosting = Node(
            library, RuntimeSecureSurface.Entrypoints, () => new RuntimeSecureSurface(library));
        Remotes = new RuntimeRemotes(library);
        SecurityRoles = new RuntimeSecurityRoles(library);
        Certificates = new RuntimeCertificates(library);

        delegate* unmanaged[Cdecl]<XmipStr*, nuint, nuint*, int> words =
            (delegate* unmanaged[Cdecl]<XmipStr*, nuint, nuint*, int>)
            Export(library, OperateAbi.StageWordsEntrypoint);
        StageWords = Words(words);
        words = (delegate* unmanaged[Cdecl]<XmipStr*, nuint, nuint*, int>)
            Export(library, OperateAbi.RoleWordsEntrypoint);
        RoleWords = Words(words);
    }

    /// <summary>The library these rules were loaded from.</summary>
    public string Source { get; }

    /// <summary>Section 8: a publication read by the runtime's one
    /// reader.</summary>
    public PublicationReader Publications { get; }

    /// <summary>Section 9: a program's audit record, recorded by the audit
    /// capability (ADR-0062).</summary>
    public RuntimeAudit Audit { get; }

    /// <summary>Section 10: the cluster's <c>xmip.toml</c> read, edited and
    /// sliced (ADR-0064, amendment 2026-10-03; ADR-0031, amendment
    /// 2026-10-05).</summary>
    public RuntimeDesign Design =>
        _design ?? throw RemoteBuild.Refused(_library, RuntimeDesign.Entrypoints);

    /// <summary>Section 11: Events, subscribed from .NET and published into
    /// this process (ADR-0065).</summary>
    public RuntimeEvents Events =>
        _events ?? throw RemoteBuild.Refused(_library, RuntimeEvents.Entrypoints);

    /// <summary>Section 12: the technologies the runtime carries and the
    /// settings each declares (ADR-0064, amendment 2026-09-26).</summary>
    public RuntimeCatalogue Catalogue =>
        _catalogue ?? throw RemoteBuild.Refused(_library, RuntimeCatalogue.Entrypoints);

    /// <summary>Section 13: a System Process declared, and the declarations
    /// that stand (ADR-0053 clause 3).</summary>
    public RuntimeProcesses Processes =>
        _processes ?? throw RemoteBuild.Refused(_library, RuntimeProcesses.Entrypoints);

    /// <summary>Section 11, what an operator lists and does: the Event
    /// subscriptions a hub holds, and pause, resume and remove on one
    /// (ADR-0065, amendment 2026-09-29).</summary>
    public RuntimeEventSubscriptions EventSubscriptions =>
        _eventSubscriptions
            ?? throw RemoteBuild.Refused(_library, RuntimeEventSubscriptions.Entrypoints);

    /// <summary>Section 14: the Subscriptions of the nodes running in this
    /// process, pause and resume on one, and an operator's order on any
    /// noun (ADR-0013, amendment 2026-09-30).</summary>
    public RuntimeSubscriptions Subscriptions =>
        _subscriptions ?? throw RemoteBuild.Refused(_library, RuntimeSubscriptions.Entrypoints);

    /// <summary>Section 15: the Dead Message Queues of the nodes running in
    /// this process, and Replay on one of their Messages (ADR-0052,
    /// amendment 2026-10-01).</summary>
    public RuntimeDeadMessages DeadMessages =>
        _deadMessages ?? throw RemoteBuild.Refused(_library, RuntimeDeadMessages.Entrypoints);

    /// <summary>Section 16: Retry and Dismiss on a Journey that failed, sent
    /// by a node running in this process (runtime-model.md section 13;
    /// ADR-0013).</summary>
    public RuntimeJourneys Journeys =>
        _journeys ?? throw RemoteBuild.Refused(_library, RuntimeJourneys.Entrypoints);

    /// <summary>Section 17: a node reached at its operate address over gRPC
    /// inside Xmip's mutual TLS, and this process's node served on an operate
    /// listener of its own (ADR-0014, amendment 2026-10-12).</summary>
    public RuntimeRemotes Remotes { get; }

    /// <summary>Section 18: a person's security role and the one check every
    /// act passes (ADR-0009).</summary>
    public RuntimeSecurityRoles SecurityRoles { get; }

    /// <summary>Section 19: a person's key and request made, and signed on a
    /// node's build by its authority (ADR-0034, amendment 2026-10-15).</summary>
    public RuntimeCertificates Certificates { get; }

    /// <summary>Section 20: the scripts that upgrade each database on a server
    /// from one schema version to a later one (ADR-0015, amendment
    /// 2026-10-20). A node's build only.</summary>
    public RuntimeDatabases Databases =>
        _databases ?? throw RemoteBuild.Refused(_library, RuntimeDatabases.Entrypoints);

    /// <summary>Section 21: a web host's HTTPS in Xmip's own TLS, and its
    /// certificate under an authority kept in a folder (ADR-0009, amendment
    /// 2026-10-21). A node's build only.</summary>
    public RuntimeSecureSurface Hosting =>
        _hosting ?? throw RemoteBuild.Refused(_library, RuntimeSecureSurface.Entrypoints);

    /// <summary>The stage words, in message-path order — the segments a
    /// scope names a stage by: <c>node::Stage::WORDS</c>, read once when the
    /// library loads.</summary>
    public IReadOnlyList<string> StageWords { get; }

    /// <summary>The words a node may declare, its roles —
    /// <c>node::NodeRole::WORDS</c>, read once when the library loads
    /// (ADR-0056, amendment 2026-10-01).</summary>
    public IReadOnlyList<string> RoleWords { get; }

    /// <summary>Section 7's, section 8's, section 9's, section 17's client,
    /// section 18's and section 19's request symbols, each of which a runtime
    /// must export, its remote build too. Sections 10 to 16, section 17's
    /// serve side and section 19's signing are a node's, bound where the
    /// library exports them (<see cref="RemoteBuild"/>).</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
    [
        OperateAbi.HealthRolledEntrypoint,
        OperateAbi.CountedWordEntrypoint,
        OperateAbi.StageCountedEntrypoint,
        OperateAbi.StagePausableEntrypoint,
        OperateAbi.StageLocationEntrypoint,
        OperateAbi.CapabilityPublishedEntrypoint,
        OperateAbi.CapabilityEntryEntrypoint,
        .. PublicationReader.Entrypoints,
        .. RuntimeAudit.Entrypoints,
        .. RuntimeRemotes.Entrypoints,
        .. RuntimeSecurityRoles.Entrypoints,
        .. RuntimeCertificates.Entrypoints,
        OperateAbi.ScopeContainsEntrypoint,
        OperateAbi.ScopeMatchesEntrypoint,
        OperateAbi.RunShownEntrypoint,
        OperateAbi.ScopePartsEntrypoint,
        OperateAbi.ScopeNodeEntrypoint,
        OperateAbi.StageWordsEntrypoint,
        OperateAbi.RoleWordsEntrypoint,
        OperateAbi.RoleDeclaredEntrypoint,
        OperateAbi.RoleStagesEntrypoint,
        OperateAbi.HealthWordEntrypoint,
        OperateAbi.HealthColorEntrypoint,
        OperateAbi.HealthNamedEntrypoint,
        OperateAbi.HealthOrderEntrypoint,
    ];

    /// <summary>Load the runtime's library and bind section 7. Null, with the
    /// reason, when there is none, it does not load, or it lacks one of
    /// <see cref="Entrypoints"/>.</summary>
    public static RuntimeRules? Load(string path, out string reason)
    {
        if (!NativeRuntime.TryLoad(path, out nint library, out reason))
        {
            return null;
        }

        foreach (string entrypoint in Entrypoints)
        {
            if (!NativeLibrary.TryGetExport(library, entrypoint, out _))
            {
                NativeRuntime.Free(library);
                reason = $"{path} does not export {entrypoint}";
                return null;
            }
        }

        return new RuntimeRules(library, path);
    }

    private static string[] Read(XmipStr* words, nuint count)
    {
        string[] read = new string[checked((int)count)];

        for (int at = 0; at < read.Length; at++)
        {
            read[at] = words[at].Read();
        }

        return read;
    }

    // A refusal longer than the buffer is cut at it: every refusal names a few
    // words and the stage words, far short of it.
    private static string Sentence(byte* said, nuint length)
    {
        return Encoding.UTF8.GetString(said, (int)Math.Min(length, (nuint)Small));
    }

    private static nint Export(nint library, string entrypoint)
    {
        return NativeLibrary.GetExport(library, entrypoint);
    }

    // A node's binding, where the library exports all of it; a remote build
    // leaves it out, and its property refuses in words (RemoteBuild).
    private static T? Node<T>(nint library, IReadOnlyList<string> entrypoints, Func<T> bind)
        where T : class
    {
        return RemoteBuild.Carries(library, entrypoints) ? bind() : null;
    }

    private static string[] Words(delegate* unmanaged[Cdecl]<XmipStr*, nuint, nuint*, int> words)
    {
        nuint count = 0;

        Check(words(null, 0, &count));

        XmipStr[] found = new XmipStr[count];

        fixed (XmipStr* into = found)
        {
            Check(words(into, count, &count));
        }

        return [.. found.Select(word => word.Read())];
    }

    // Any other status is a runtime breaking section 7's promise, not an answer.
    private static void Check(int status)
    {
        if (status != 0)
        {
            throw new InvalidOperationException(
                $"the runtime answered a section 7 rule with {(XmipStatus)status}");
        }
    }
}
