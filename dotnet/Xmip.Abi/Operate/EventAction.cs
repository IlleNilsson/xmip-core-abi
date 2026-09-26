namespace Xmip.Abi.Operate;

/// <summary>
/// The action an Event completed (runtime-model.md section 17). The values
/// are the header's <c>XMIP_ACTION_*</c> (section 11).
/// </summary>
public enum EventAction
{
    /// <summary>Receive.</summary>
    Receive = 0,

    /// <summary>Process.</summary>
    Process = 1,

    /// <summary>Send.</summary>
    Send = 2,
}
