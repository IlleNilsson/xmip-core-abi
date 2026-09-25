using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 8 of <c>include/xmip_operate.h</c>, crossed by P/Invoke: a
/// publication — the file a node or a roll publishes its snapshot to — read by
/// the runtime's one reader, <c>observe::Publication</c>, and a curve — the
/// history file beside it — by <c>observe::Curve</c> (<see cref="Curve"/>). A surface that reads
/// a file hands the text here and gets a <see cref="Publication"/> back; the
/// file's keys, its words and what an unknown word falls back to are written
/// once, in Rust, and nowhere in .NET (ADR-0052 and ADR-0027, amendments
/// 2026-09-24; open problem 25).
/// </summary>
/// <remarks>
/// Each read holds a handle only for as long as it takes to copy what it
/// read out, then frees it; nothing borrowed outlives the call. One instance
/// serves a whole process from any thread.
/// </remarks>
public sealed unsafe class PublicationReader
{
    // A reader's report is a parser's sentence; this holds any of them, and a
    // longer one is asked for again at its length.
    private const int Report = 1024;

    private readonly delegate* unmanaged[Cdecl]<XmipStr, nint*, byte*, nuint, nuint*, int> _read;
    private readonly delegate* unmanaged[Cdecl]<nint, void> _free;
    private readonly delegate* unmanaged[Cdecl]<nint, XmipPublicationHead*, int> _head;
    private readonly delegate* unmanaged[Cdecl]<nint, XmipHealthEntry*, nuint, nuint*, int> _records;
    private readonly delegate* unmanaged[Cdecl]<nint, XmipMeasurement*, nuint, nuint*, int> _counts;
    private readonly delegate* unmanaged[Cdecl]<nint, XmipTopologyNode*, nuint, nuint*, int> _nodes;
    private readonly delegate* unmanaged[Cdecl]<nint, XmipTopologyLink*, nuint, nuint*, int> _links;
    private readonly delegate* unmanaged[Cdecl]<nint, uint, XmipStr*, nuint, nuint*, int> _run;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, nint*, byte*, nuint, nuint*, int> _curve;
    private readonly delegate* unmanaged[Cdecl]<nint, XmipMeasurement*, nuint, nuint*, int> _points;
    private readonly delegate* unmanaged[Cdecl]<nint, void> _curveFree;
    private readonly delegate* unmanaged[Cdecl]<int, XmipStr*, XmipStr*, int> _kindWords;
    private readonly delegate* unmanaged[Cdecl]<int, XmipStr*, XmipStr*, int> _originWords;
    private readonly delegate* unmanaged[Cdecl]<int, XmipStr*, XmipStr*, int> _patternWords;

    internal PublicationReader(nint library)
    {
        _read = (delegate* unmanaged[Cdecl]<XmipStr, nint*, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.PublicationReadEntrypoint);
        _free = (delegate* unmanaged[Cdecl]<nint, void>)
            NativeLibrary.GetExport(library, OperateAbi.PublicationFreeEntrypoint);
        _head = (delegate* unmanaged[Cdecl]<nint, XmipPublicationHead*, int>)
            NativeLibrary.GetExport(library, OperateAbi.PublicationHeadEntrypoint);
        _records = (delegate* unmanaged[Cdecl]<nint, XmipHealthEntry*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.PublicationRecordsEntrypoint);
        _counts = (delegate* unmanaged[Cdecl]<nint, XmipMeasurement*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.PublicationCountsEntrypoint);
        _nodes = (delegate* unmanaged[Cdecl]<nint, XmipTopologyNode*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.PublicationNodesEntrypoint);
        _links = (delegate* unmanaged[Cdecl]<nint, XmipTopologyLink*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.PublicationLinksEntrypoint);
        _run = (delegate* unmanaged[Cdecl]<nint, uint, XmipStr*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.PublicationRunEntrypoint);
        _curve = (delegate* unmanaged[Cdecl]<XmipStr, nint*, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.CurveReadEntrypoint);
        _points = (delegate* unmanaged[Cdecl]<nint, XmipMeasurement*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.CurvePointsEntrypoint);
        _curveFree = (delegate* unmanaged[Cdecl]<nint, void>)
            NativeLibrary.GetExport(library, OperateAbi.CurveFreeEntrypoint);
        _kindWords = (delegate* unmanaged[Cdecl]<int, XmipStr*, XmipStr*, int>)
            NativeLibrary.GetExport(library, OperateAbi.TopologyKindWordsEntrypoint);
        _originWords = (delegate* unmanaged[Cdecl]<int, XmipStr*, XmipStr*, int>)
            NativeLibrary.GetExport(library, OperateAbi.TopologyOriginWordsEntrypoint);
        _patternWords = (delegate* unmanaged[Cdecl]<int, XmipStr*, XmipStr*, int>)
            NativeLibrary.GetExport(library, OperateAbi.TopologyPatternWordsEntrypoint);
    }

    /// <summary>Section 8's symbols, each of which a runtime must export.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
    [
        OperateAbi.PublicationReadEntrypoint,
        OperateAbi.PublicationFreeEntrypoint,
        OperateAbi.PublicationHeadEntrypoint,
        OperateAbi.PublicationRecordsEntrypoint,
        OperateAbi.PublicationCountsEntrypoint,
        OperateAbi.PublicationNodesEntrypoint,
        OperateAbi.PublicationLinksEntrypoint,
        OperateAbi.PublicationRunEntrypoint,
        OperateAbi.CurveReadEntrypoint,
        OperateAbi.CurvePointsEntrypoint,
        OperateAbi.CurveFreeEntrypoint,
        OperateAbi.TopologyKindWordsEntrypoint,
        OperateAbi.TopologyOriginWordsEntrypoint,
        OperateAbi.TopologyPatternWordsEntrypoint,
    ];

    /// <summary>
    /// The publication <paramref name="text"/> is, as the runtime reads it —
    /// or null, with the reader's own words in <paramref name="refusal"/>,
    /// when the text is no publication.
    /// </summary>
    public Publication? Read(string text, out string refusal)
    {
        nint handle = Opened(_read, text, out refusal);

        if (handle == 0)
        {
            return null;
        }

        try
        {
            return Copied(handle);
        }
        finally
        {
            _free(handle);
        }
    }

    /// <summary>
    /// The points of the curve <paramref name="text"/> is — a node's
    /// throughput over time (ADR-0029), each a measurement at the curve's node
    /// whose window is the instant it was observed — as the runtime reads it;
    /// or null, with the reader's own words in <paramref name="refusal"/>,
    /// when the text is no curve.
    /// </summary>
    public IReadOnlyList<MeasurementRecord>? Curve(string text, out string refusal)
    {
        nint handle = Opened(_curve, text, out refusal);

        if (handle == 0)
        {
            return null;
        }

        try
        {
            return [.. Fill(_points, handle).Select(Count)];
        }
        finally
        {
            _curveFree(handle);
        }
    }

    /// <summary>What a topology kind is called — <c>observe::NodeKind</c>'s
    /// word and name. Null for a value the runtime does not define.</summary>
    public TopologyWord? Words(TopologyNodeKind kind)
    {
        return Said(_kindWords, (int)kind);
    }

    /// <summary>What a topology origin is called — <c>observe::Origin</c>'s
    /// word and name. Null for a value the runtime does not define.</summary>
    public TopologyWord? Words(TopologyOrigin origin)
    {
        return Said(_originWords, (int)origin);
    }

    /// <summary>What a communication pattern is called —
    /// <c>observe::Pattern</c>'s word and name. Null for a value the runtime
    /// does not define.</summary>
    public TopologyWord? Words(CommunicationPattern pattern)
    {
        return Said(_patternWords, (int)pattern);
    }

    private static TopologyWord? Said(
        delegate* unmanaged[Cdecl]<int, XmipStr*, XmipStr*, int> words, int value)
    {
        XmipStr word;
        XmipStr name;

        return words(value, &word, &name) == 0
            ? new TopologyWord(word.Read(), name.Read())
            : null;
    }

    // A handle from one of section 8's readers, or 0 with the refusal.
    private static nint Opened(
        delegate* unmanaged[Cdecl]<XmipStr, nint*, byte*, nuint, nuint*, int> open,
        string text,
        out string refusal)
    {
        ArgumentNullException.ThrowIfNull(text);

        byte[] bytes = Encoding.UTF8.GetBytes(text);
        byte[] report = new byte[Report];
        nint handle = 0;
        nuint length = 0;
        int status;

        fixed (byte* data = bytes)
        {
            XmipStr source = new(data, (nuint)bytes.Length);

            fixed (byte* said = report)
            {
                status = open(source, &handle, said, (nuint)report.Length, &length);
            }

            if (status == (int)XmipStatus.Invalid && length > (nuint)report.Length)
            {
                report = new byte[checked((int)length)];

                fixed (byte* said = report)
                {
                    status = open(source, &handle, said, (nuint)report.Length, &length);
                }
            }
        }

        if (status == (int)XmipStatus.Invalid)
        {
            refusal = Encoding.UTF8.GetString(report, 0, (int)Math.Min(length, (nuint)report.Length));
            return 0;
        }

        Check(status);
        refusal = string.Empty;
        return handle;
    }

    private Publication Copied(nint handle)
    {
        XmipPublicationHead head;

        Check(_head(handle, &head));

        string source = head.Source.Read();

        return new Publication(
            source,
            head.Node.Read(),
            [.. Fill(_records, handle).Select(Record)],
            [.. Fill(_counts, handle).Select(Count)],
            head.HasTopology == 0 ? null : Topology(handle, head, source),
            head.HasRun == 0 ? null : Run(handle, head));
    }

    private TopologySnapshot Topology(nint handle, XmipPublicationHead head, string source)
    {
        string drawnBy = head.TopologySource.Read();

        return new TopologySnapshot(
            [.. Fill(_nodes, handle).Select(Node)],
            [.. Fill(_links, handle).Select(Link)],
            Operator.FromNanos(head.TopologyObservedUnixNanos),
            drawnBy.Length > 0 ? drawnBy : source);
    }

    private PublishedRun Run(nint handle, XmipPublicationHead head)
    {
        return new PublishedRun(
            head.Cluster.Read(),
            Listed(handle, RunList.Tests),
            Listed(handle, RunList.Nodes),
            Listed(handle, RunList.Capabilities),
            Listed(handle, RunList.Online),
            head.Stress.Read());
    }

    private string[] Listed(nint handle, RunList list)
    {
        nuint count = 0;

        Check(_run(handle, (uint)list, null, 0, &count));

        XmipStr[] words = new XmipStr[count];

        fixed (XmipStr* into = words)
        {
            Check(_run(handle, (uint)list, into, count, &count));
        }

        return [.. words.Select(word => word.Read())];
    }

    private static T[] Fill<T>(
        delegate* unmanaged[Cdecl]<nint, T*, nuint, nuint*, int> fill, nint handle)
        where T : unmanaged
    {
        nuint count = 0;

        Check(fill(handle, null, 0, &count));

        T[] found = new T[count];

        fixed (T* into = found)
        {
            Check(fill(handle, into, count, &count));
        }

        return found;
    }

    private static HealthRecord Record(XmipHealthEntry entry)
    {
        return new HealthRecord(
            entry.Scope.Read(),
            (HealthState)entry.Health,
            entry.Severity,
            entry.Evidence.Read(),
            Operator.FromNanos(entry.ObservedUnixNanos));
    }

    private static MeasurementRecord Count(XmipMeasurement entry)
    {
        return new MeasurementRecord(
            entry.Scope.Read(),
            (Counted)entry.Counted,
            entry.Value,
            Operator.FromNanos(entry.WindowStartUnixNanos),
            Operator.FromNanos(entry.WindowEndUnixNanos),
            Operator.FromNanos(entry.ObservedUnixNanos));
    }

    private static TopologyNode Node(XmipTopologyNode entry)
    {
        string parent = entry.Parent.Read();

        return new TopologyNode(
            entry.Id.Read(),
            parent.Length > 0 ? parent : null,
            entry.Label.Read(),
            (TopologyNodeKind)entry.Kind,
            entry.Scope.Read(),
            (HealthState)entry.Health,
            (TopologyOrigin)entry.Origin,
            entry.Load,
            entry.Activity,
            entry.Evidence.Read());
    }

    private static CommunicationLink Link(XmipTopologyLink entry)
    {
        return new CommunicationLink(
            entry.Id.Read(),
            entry.From.Read(),
            entry.To.Read(),
            (CommunicationPattern)entry.Pattern,
            (TopologyOrigin)entry.Origin,
            entry.Protocol.Read(),
            (HealthState)entry.Health,
            entry.Volume,
            entry.Rate,
            entry.LatencyMilliseconds,
            entry.Progress,
            entry.Attempts,
            entry.Evidence.Read());
    }

    // Any other status is a runtime breaking section 8's promise, not an answer.
    private static void Check(int status)
    {
        if (status != 0)
        {
            throw new InvalidOperationException(
                $"the runtime answered a section 8 read with {(XmipStatus)status}");
        }
    }
}
