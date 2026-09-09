using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// What the runtime said about a proposed node configuration. Section 6 of
/// <c>xmip_operate.h</c>: <see cref="XmipStatus.Ok"/> with no problems means
/// the configuration is good; <see cref="XmipStatus.Invalid"/> with a report
/// means it is not, one problem per line; anything else is about the call
/// rather than the document.
/// </summary>
public sealed record ValidationRecord(XmipStatus Status, IReadOnlyList<string> Problems)
{
    /// <summary>The configuration is good: the runtime validated it and found
    /// nothing to say.</summary>
    public bool IsValid => Status == XmipStatus.Ok && Problems.Count == 0;
}
