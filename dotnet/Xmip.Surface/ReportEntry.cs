using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// Every Report, as the runtime lists them (<c>xmip_operate.h</c> section
/// 25; ADR-0072, amendment 2026-11-10): one list, each entry saying whether
/// it is quick — it presents data that already exists — or takes longer —
/// it presents what a Quest probes first. The list, the kinds and every word
/// are <c>xmip-core-report</c>'s; this holds them for every .NET surface,
/// read by <see cref="ReportCatalog"/>.
/// </summary>
/// <param name="Kinds">Both kinds, each with the words a surface shows it
/// by.</param>
/// <param name="Entries">Every Report, in the order the list shows them; or
/// the one asked for by its name.</param>
/// <param name="Unread">Why each Quest's last probe could not be read, the
/// runtime's sentence opening FAILED, where it could not; every Report is
/// listed all the same.</param>
/// <param name="Json">The runtime's answer whole, for a program.</param>
public sealed record ReportListing(
    IReadOnlyList<ReportKindWords> Kinds,
    IReadOnlyList<ReportEntry> Entries,
    string? Unread,
    string Json);

/// <summary>One kind of Report and the words a surface shows it by, the
/// runtime's: its <paramref name="Word"/> (<c>existing-data</c>,
/// <c>quest</c>), its <paramref name="Name"/> (<c>Quick</c>, <c>Takes
/// longer</c>) and its <paramref name="Meaning"/>.</summary>
public sealed record ReportKindWords(ReportKind Kind, string Word, string Name, string Meaning);

/// <summary>
/// One Report of the list. Its <paramref name="Kind"/> follows from what it
/// holds, never from its name, and <paramref name="Words"/> are the words a
/// surface shows it by, the list's own. An entry not
/// <paramref name="Available"/> yet is
/// listed and has nothing to open. A quick entry names the entrypoint of the
/// reader that presents it, <paramref name="ReadBy"/>, which each surface
/// opens at its own command for that reader; a Quest's names its
/// <paramref name="Parameter"/>, what a row of its second level is called,
/// <paramref name="Row"/>, its <paramref name="Levels"/>, and its last
/// probe's <paramref name="TookMilliseconds"/> and
/// <paramref name="ProbedAt"/>, read back from the audit the probe was
/// recorded in.
/// </summary>
/// <param name="Name">Its name, as it is opened by.</param>
/// <param name="Covers">What it covers, in a sentence.</param>
/// <param name="Kind">What it presents.</param>
/// <param name="Words">Its kind's words: the name <c>Quick</c> or <c>Takes
/// longer</c>, the word and the meaning, as the list gives them.</param>
/// <param name="Available">Whether it is built; false says it is not yet
/// available.</param>
/// <param name="Expect">What to expect, in words: <c>last probe took 4
/// ms</c>, <c>not probed yet</c> or <c>not yet available</c>; null for a
/// quick entry available.</param>
/// <param name="ReadBy">The entrypoint of the reader that presents a quick
/// entry; null otherwise.</param>
/// <param name="Parameter">What a Quest's entry is opened for; null where it
/// takes none.</param>
/// <param name="Row">What a row of a Quest's second level is called.</param>
/// <param name="Levels">A Quest's levels, each said.</param>
/// <param name="TookMilliseconds">How long its last probe took.</param>
/// <param name="ProbedAt">When its last probe was.</param>
public sealed record ReportEntry(
    string Name,
    string Covers,
    ReportKind Kind,
    ReportKindWords Words,
    bool Available,
    string? Expect,
    string? ReadBy,
    ReportParameter? Parameter,
    string? Row,
    IReadOnlyList<string> Levels,
    long? TookMilliseconds,
    DateTimeOffset? ProbedAt);

/// <summary>What a Quest's Report is opened for: the parameter's
/// <paramref name="Name"/> — <c>regulation</c> — and its
/// <paramref name="Choices"/> — <c>GDPR</c>.</summary>
public sealed record ReportParameter(string Name, IReadOnlyList<string> Choices);
