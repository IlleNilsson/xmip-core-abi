namespace Xmip.Abi.Operate;

/// <summary>
/// The mood of a scope — observability-model.md section 6. A mood, not a colour
/// (the surface paints it): what a human gets out of a resource under load. Five
/// leaf moods, worsening, then Holding, the rollup (ADR-0041). The values are
/// the header's <c>XMIP_HEALTH_*</c>.
/// </summary>
public enum HealthState
{
    /// <summary>Results flowing, at ease.</summary>
    Fine = 0,

    /// <summary>A deliberate hold — an operator is working on it.</summary>
    Paused = 1,

    /// <summary>Handling the load.</summary>
    Working = 2,

    /// <summary>Strained — change the load.</summary>
    Stressed = 3,

    /// <summary>Spent — replace the hardware.</summary>
    Exhausted = 4,

    /// <summary>Blocked or failed — the pain (a cert, a password, a folder).</summary>
    Done = 5,

    /// <summary>Rollup only: a parent with something not-Fine beneath it.</summary>
    Holding = 6,
}
