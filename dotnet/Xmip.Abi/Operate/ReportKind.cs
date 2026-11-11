namespace Xmip.Abi.Operate;

/// <summary>
/// What a Report presents, and so whether it is quick or takes longer
/// (ADR-0072, amendment 2026-11-10). A Report presents existing data; a
/// Quest probes for data to present in a Report (doc/terminology.md). The
/// kind follows from what an entry of the list holds, never from its name;
/// the words a surface shows it by are the runtime's, in the list's
/// <c>kinds</c>. The values are the header's <c>XMIP_REPORT_*</c> (section
/// 25).
/// </summary>
public enum ReportKind
{
    /// <summary>Data that already exists: quick.</summary>
    ExistingData = 0,

    /// <summary>What a Quest probes for first: it takes longer.</summary>
    Quest = 1,
}
