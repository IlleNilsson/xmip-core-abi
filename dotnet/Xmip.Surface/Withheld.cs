using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// What a face reads where every cluster it holds is hidden and it did not
/// ask to include them (ADR-0028 and ADR-0052, amendments 2026-09-30): no
/// records, no figures, and a source that says why and how to see them. A
/// face never shows a hidden cluster unasked, and never an error page either
/// (ADR-0052: a surface that cannot reach anything says so in
/// <see cref="Source"/> and reports nothing).
/// </summary>
internal sealed class Withheld : IOperatorSurface
{
    private Withheld()
    {
    }

    /// <summary>The one there is; it holds nothing.</summary>
    public static Withheld Surface { get; } = new();

    /// <inheritdoc />
    public string Source =>
        "every cluster held is a hidden test run — tick show test clusters to see it";

    /// <inheritdoc />
    public IReadOnlyList<HealthRecord> Health(string scope)
    {
        return [];
    }

    /// <inheritdoc />
    public MeasurementRecord? Measure(string scope, Counted counted)
    {
        return null;
    }

    /// <inheritdoc />
    public string PauseScope(string scope, string who)
    {
        return $"REFUSED. {Source}.";
    }

    /// <inheritdoc />
    public string ResumeScope(string scope)
    {
        return $"REFUSED. {Source}.";
    }
}
