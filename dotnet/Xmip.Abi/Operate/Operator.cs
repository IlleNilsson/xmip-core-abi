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
/// own (ADR-0014, amendment of 2026-08-26). What it asks has no node behind
/// it — starting a plan and validating a document; a running node is
/// operated over gRPC at its operate listener (<see cref="RuntimeRemotes"/>;
/// ADR-0014, amendment 2026-10-13).
/// </summary>
/// <remarks>
/// Every method answers in the header's terms — a status, a record — and
/// renders nothing. Words for an operator are the surface's business.
/// </remarks>
public sealed unsafe class Operator : IDisposable
{
    private readonly nint _library;
    private readonly XmipOperate _table;
    private bool _disposed;

    /// <summary>The library this table came from, for a surface to show.</summary>
    public string Source { get; }

    private Operator(nint library, XmipOperate table, string path)
    {
        _library = library;
        _table = table;
        Source = path;
    }

    /// <summary>
    /// Load the runtime's native library and take its operator table.
    /// Returns <c>null</c>, with the reason, when it cannot — a surface that
    /// cannot reach a node shows that rather than an empty tree.
    /// </summary>
    public static Operator? Load(string path, out string reason)
    {
        if (!NativeRuntime.TryLoad(path, out nint library, out reason))
        {
            return null;
        }

        if (!NativeLibrary.TryGetExport(library, OperateAbi.Entrypoint, out nint symbol))
        {
            NativeRuntime.Free(library);
            reason = $"{path} does not export {OperateAbi.Entrypoint}";
            return null;
        }

        delegate* unmanaged[Cdecl]<uint, XmipOperate*, int> entry =
            (delegate* unmanaged[Cdecl]<uint, XmipOperate*, int>)symbol;
        XmipOperate table;
        int status = entry(OperateAbi.Version, &table);

        if (status != 0)
        {
            NativeRuntime.Free(library);
            reason = $"{OperateAbi.Entrypoint} refused version {OperateAbi.Version} " +
                $"with status {status}";
            return null;
        }

        reason = string.Empty;
        return new Operator(library, table, path);
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

    /// <summary>A header timestamp — nanoseconds since the Unix epoch — as a
    /// .NET time; the one conversion every binding reads a time by.</summary>
    internal static DateTimeOffset FromNanos(long nanos)
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

        NativeRuntime.Free(_library);
    }
}
