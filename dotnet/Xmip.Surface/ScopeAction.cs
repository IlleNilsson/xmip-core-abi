namespace Xmip.Surface;

/// <summary>
/// What an operator may do to a scope through the boundary: the two acts
/// <c>xmip_operate.h</c> carries (ADR-0027 clause 5). There is no start,
/// stop or restart here — the thing that watches must not be able to stop
/// the thing it watches, and the boundary has no such call.
/// </summary>
public enum ScopeAction
{
    /// <summary>Pause everything at and beneath the scope.</summary>
    Pause,

    /// <summary>Resume everything at and beneath the scope.</summary>
    Resume,
}
