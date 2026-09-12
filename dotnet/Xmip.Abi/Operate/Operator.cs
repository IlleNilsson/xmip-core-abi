using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// The operator boundary as <c>include/xmip_operate.h</c> declares it, crossed
/// by P/Invoke. ADR-0012 clause 1 applies here too: the header is normative
/// and this is not; where they differ, the header is right and this is a
/// defect. One binding for every surface — the cli, the PowerShell module and
/// the GUI all hold one of these, and none of them declares a struct of its
/// own (ADR-0014, amendment of 2026-08-26).
/// </summary>
/// <remarks>
/// Every method answers in the header's terms — a status, a record — and
/// renders nothing. Words for an operator are the surface's business.
/// </remarks>
public sealed unsafe class Operator : IDisposable
{
    private readonly nint _library;
    private readonly XmipOperate _table;
    private readonly delegate* unmanaged[Cdecl]<ulong, uint, ulong*, int> _waitChange;
    private bool _disposed;

    // The header says entries are valid until the next call on the table, and
    // a Blazor Server app calls from a prerender thread and a circuit thread.
    // Two callers at once would have one reading pointers the other has just
    // replaced. One call at a time, and every string is copied out before the
    // lock is released.
    private readonly Lock _gate = new();

    /// <summary>The library this table came from, for a surface to show.</summary>
    public string Source { get; }

    private Operator(
        nint library,
        XmipOperate table,
        string path,
        delegate* unmanaged[Cdecl]<ulong, uint, ulong*, int> waitChange)
    {
        _library = library;
        _table = table;
        _waitChange = waitChange;
        Source = path;
    }

    /// <summary>
    /// Load the runtime's native library and take its operator table.
    /// Returns <c>null</c>, with the reason, when it cannot — a surface that
    /// cannot reach a node shows that rather than an empty tree.
    /// </summary>
    public static Operator? Load(string path, out string reason)
    {
        if (!File.Exists(path))
        {
            reason = $"no runtime library at {path}";
            return null;
        }

        // Load a copy, never the build output itself. A loaded library is
        // locked for as long as this process lives, and the path configured in
        // development is the runtime's own target/debug — so every GUI left
        // running made the next `cargo build` fail with a locked .dll, and the
        // fix was always "stop the GUI first". Copying costs one file write and
        // removes the hazard for good.
        string copy = Path.Combine(
            Path.GetTempPath(),
            $"xmip-abi-{Guid.NewGuid():n}-{Path.GetFileName(path)}");
        File.Copy(path, copy);

        if (!NativeLibrary.TryLoad(copy, out nint library))
        {
            reason = $"{path} could not be loaded";
            return null;
        }

        if (!NativeLibrary.TryGetExport(library, OperateAbi.Entrypoint, out nint symbol))
        {
            NativeLibrary.Free(library);
            reason = $"{path} does not export {OperateAbi.Entrypoint}";
            return null;
        }

        delegate* unmanaged[Cdecl]<uint, XmipOperate*, int> entry =
            (delegate* unmanaged[Cdecl]<uint, XmipOperate*, int>)symbol;
        XmipOperate table;
        int status = entry(OperateAbi.Version, &table);

        if (status != 0)
        {
            NativeLibrary.Free(library);
            reason = $"{OperateAbi.Entrypoint} refused version {OperateAbi.Version} " +
                $"with status {status}";
            return null;
        }

        delegate* unmanaged[Cdecl]<ulong, uint, ulong*, int> waitChange = null;

        if (NativeLibrary.TryGetExport(library, OperateAbi.ChangeEntrypoint, out nint changeSymbol))
        {
            waitChange = (delegate* unmanaged[Cdecl]<ulong, uint, ulong*, int>)changeSymbol;
        }

        reason = string.Empty;
        return new Operator(library, table, path, waitChange);
    }

    /// <summary>
    /// Start a node from its saved configuration file — as far as the runtime
    /// can today, which is read, build, validate and plan. Section 6 of the
    /// header. The table's next read shows the result either way;
    /// <see cref="XmipStatus.Unsupported"/> when this runtime does not export
    /// <see cref="OperateAbi.StartEntrypoint"/>.
    /// </summary>
    public XmipStatus Start(string configurationPath)
    {
        if (!NativeLibrary.TryGetExport(_library, OperateAbi.StartEntrypoint, out nint symbol))
        {
            return XmipStatus.Unsupported;
        }

        delegate* unmanaged[Cdecl]<XmipStr, int> start =
            (delegate* unmanaged[Cdecl]<XmipStr, int>)symbol;

        using PinnedStr path = XmipStr.Pin(configurationPath);

        return (XmipStatus)start(path.Value);
    }

    /// <summary>
    /// Validate configuration text without applying it — a proposed document,
    /// checked against the same runtime that would start it, publishing
    /// nothing. Section 6 of the header; ADR-0027 clause 9. The runtime is
    /// asked once for the report's length and once for the report, so nothing
    /// is truncated silently. <see cref="XmipStatus.Unsupported"/> when this
    /// runtime does not export <see cref="OperateAbi.ValidateEntrypoint"/>.
    /// </summary>
    public ValidationRecord Validate(string configuration)
    {
        if (!NativeLibrary.TryGetExport(_library, OperateAbi.ValidateEntrypoint, out nint symbol))
        {
            return new ValidationRecord(XmipStatus.Unsupported, []);
        }

        delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int> validate =
            (delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int>)symbol;

        using PinnedStr text = XmipStr.Pin(configuration);

        nuint needed = 0;
        int status = validate(text.Value, null, 0, &needed);

        if (needed == 0)
        {
            return new ValidationRecord((XmipStatus)status, []);
        }

        byte[] report = new byte[needed];

        fixed (byte* buffer = report)
        {
            status = validate(text.Value, buffer, needed, &needed);
        }

        int length = checked((int)Math.Min(needed, (nuint)report.Length));
        string[] problems = Encoding.UTF8.GetString(report, 0, length)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new ValidationRecord((XmipStatus)status, problems);
    }

    /// <summary>Health at and beneath a scope, worst first. Empty when the
    /// scope names nothing.</summary>
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        List<HealthRecord> found = [];

        using PinnedStr text = XmipStr.Pin(scope);
        using Lock.Scope held = _gate.EnterScope();

        nuint needed = 0;

        // Ask for the count first, then for exactly that many. The header
        // promises the true count in out_len however small the buffer.
        int probe = _table.Health(_table.Ctx, text.Value, null, 0, &needed);

        if (probe != 0 || needed == 0)
        {
            return found;
        }

        XmipHealthEntry[] entries = new XmipHealthEntry[needed];
        nuint filled;

        fixed (XmipHealthEntry* buffer = entries)
        {
            _ = _table.Health(_table.Ctx, text.Value, buffer, needed, &filled);
        }

        int count = checked((int)Math.Min(filled, needed));

        for (int i = 0; i < count; i++)
        {
            XmipHealthEntry entry = entries[i];

            found.Add(new HealthRecord(
                entry.Scope.Read(),
                (HealthState)entry.Health,
                entry.Severity,
                entry.Evidence.Read(),
                FromNanos(entry.ObservedUnixNanos)));
        }

        return found;
    }

    /// <summary>Whether this runtime can wake observers when it publishes.</summary>
    public bool SupportsChangeNotifications => _waitChange != null;

    /// <summary>The revision already published, or zero for an older runtime.</summary>
    public ulong CurrentRevision => WaitForChange(0, 0) ?? 0;

    /// <summary>
    /// Wait for a publication newer than <paramref name="afterRevision"/>.
    /// Null means timeout, refusal, or a runtime predating the signal.
    /// This waits only on the publication clock; it never enters execution.
    /// </summary>
    public ulong? WaitForChange(ulong afterRevision, uint timeoutMilliseconds)
    {
        if (_waitChange == null)
        {
            return null;
        }

        ulong revision = afterRevision;
        int status = _waitChange(afterRevision, timeoutMilliseconds, &revision);

        return status == 0 && revision > afterRevision ? revision : null;
    }

    /// <summary>One kind of count, summed over the scope. Null when the scope
    /// names nothing or has no such count.</summary>
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        using PinnedStr text = XmipStr.Pin(scope);
        using Lock.Scope held = _gate.EnterScope();

        XmipMeasurement entry;
        nuint len = 0;

        int status = _table.Measure(_table.Ctx, text.Value, (int)counted, &entry, 1, &len);

        return status != 0 || len == 0
            ? null
            : new MeasurementRecord(
                entry.Scope.Read(),
                (Counted)entry.Counted,
                entry.Value,
                FromNanos(entry.WindowStartUnixNanos),
                FromNanos(entry.WindowEndUnixNanos),
                FromNanos(entry.ObservedUnixNanos));
    }

    /// <summary>Pause everything at and beneath a scope, by <paramref name="who"/>.
    /// <see cref="XmipStatus.NotFound"/> when the scope names nothing.</summary>
    public XmipStatus PauseScope(string scope, string who)
    {
        using PinnedStr scopeText = XmipStr.Pin(scope);
        using PinnedStr whoText = XmipStr.Pin(who);
        using Lock.Scope held = _gate.EnterScope();

        return (XmipStatus)_table.Pause(_table.Ctx, scopeText.Value, whoText.Value);
    }

    /// <summary>Resume everything at and beneath a scope.
    /// <see cref="XmipStatus.NotFound"/> when the scope names nothing.</summary>
    public XmipStatus ResumeScope(string scope)
    {
        using PinnedStr text = XmipStr.Pin(scope);
        using Lock.Scope held = _gate.EnterScope();

        return (XmipStatus)_table.Resume(_table.Ctx, text.Value);
    }

    private static DateTimeOffset FromNanos(long nanos)
    {
        return DateTimeOffset.UnixEpoch.AddTicks(nanos / 100);
    }

    /// <summary>Release the table, then the library. After this, nothing
    /// borrowed from either is valid.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_table.Destroy is not null)
        {
            _table.Destroy(_table.Ctx);
        }

        NativeLibrary.Free(_library);
    }
}
