namespace Xmip.Surface;

/// <summary>
/// A Report opened: what its Quest probed and found, at the levels the
/// Quest reads at (ADR-0072 and its amendment 2026-11-10). The Regulation
/// Report's three: the regulation with its tally, a row per requirement,
/// and, where a row was named, one requirement in full. Every state, kind
/// and word is the runtime's, and so is what a row is called; nothing here
/// keeps a list of them. What a Quest does not answer is left empty, never
/// made up: no title, no tally, no row.
/// </summary>
/// <param name="Report">The Report opened.</param>
/// <param name="Parameter">The choice it was opened for, <c>GDPR</c>; null
/// where it takes none.</param>
/// <param name="Title">What was probed, said; empty where the Quest says
/// nothing of it.</param>
/// <param name="Node">The node whose configuration was probed.</param>
/// <param name="Row">What one row is called: <c>requirement</c>.</param>
/// <param name="TookMilliseconds">How long the probe took.</param>
/// <param name="Tally">How many rows are in each state, each state in the
/// runtime's words and order; empty where the Quest tallies none.</param>
/// <param name="Rows">A row per requirement, each with its state.</param>
/// <param name="Requirement">The row named, in full; null where none
/// was.</param>
/// <param name="Json">The runtime's answer whole, for a program.</param>
public sealed record ReportProbed(
    string Report,
    string? Parameter,
    string Title,
    string Node,
    string Row,
    long TookMilliseconds,
    IReadOnlyList<ReportCount> Tally,
    IReadOnlyList<ReportRow> Rows,
    ReportRequirement? Requirement,
    string Json);

/// <summary>How many rows of a probed Report are in one
/// <paramref name="State"/>, the runtime's words for it — evidence, gap,
/// not applicable: its <paramref name="Count"/>.</summary>
public sealed record ReportCount(string State, int Count)
{
    /// <summary>The state and its count: <c>gap 8</c>.</summary>
    public override string ToString()
    {
        return $"{State} {Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }
}

/// <summary>One row of a probed Report's second level: a requirement's
/// <paramref name="Name"/>, its <paramref name="Citation"/> and its
/// <paramref name="State"/> — evidence, gap or not applicable, the runtime's
/// words.</summary>
public sealed record ReportRow(string Name, string Citation, string State);

/// <summary>One requirement in full: what was found
/// (<paramref name="Says"/>), what is <paramref name="Behind"/> it, and the
/// <paramref name="Actions"/> that close it.</summary>
public sealed record ReportRequirement(
    string Name,
    string Citation,
    string State,
    string Says,
    IReadOnlyList<ReportBehind> Behind,
    IReadOnlyList<ReportAction> Actions);

/// <summary>One thing behind a finding: its <paramref name="Kind"/> —
/// <c>location</c>, <c>stream</c>, <c>setting</c> — and what the runtime said
/// of it, member by member, in its order.</summary>
public sealed record ReportBehind(
    string Kind, IReadOnlyList<KeyValuePair<string, string>> Fields)
{
    /// <summary>The kind and each member's value: <c>location: receive
    /// in</c>.</summary>
    public override string ToString()
    {
        return $"{Kind}: {string.Join(" ", Fields.Select(field => field.Value))}";
    }
}

/// <summary>One action that closes a finding: its <paramref name="Kind"/> —
/// <c>configure</c>, <c>outside</c>, <c>not-yet-available</c> — the
/// <paramref name="Setting"/> it configures where it is one, and what it
/// <paramref name="Says"/>.</summary>
public sealed record ReportAction(string Kind, string? Setting, string Says)
{
    /// <summary>The kind, the setting where there is one, and what it
    /// says.</summary>
    public override string ToString()
    {
        return Setting is { } setting ? $"{Kind} {setting}: {Says}" : $"{Kind}: {Says}";
    }
}
