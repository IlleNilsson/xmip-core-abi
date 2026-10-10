namespace Xmip.Surface;

/// <summary>
/// What a surface asks of the audit (ADR-0062, amendment 2026-09-29), in the
/// words every surface uses — the command line's arguments, the PowerShell
/// parameters and the Audit view's address are these names. The meaning of
/// each is the audit capability's (<c>audit::audit_query::AuditQuery</c>),
/// reached through <see cref="ProgramAudit.Read"/>; this only carries them.
/// </summary>
/// <remarks>
/// Who a record is, is the location its process declared (ADR-0053 clause
/// 3): <see cref="Location"/> asks for a scope and everything beneath it,
/// <see cref="Host"/> for the records of programs that declared none on one
/// machine, <see cref="Program"/> for one program. Nothing is read out of a
/// program's name.
/// </remarks>
public sealed record AuditQuery
{
    /// <summary>The word the audit capability, and every address that
    /// reproduces a view, carries the choice to include what is hidden
    /// under.</summary>
    public const string HiddenKey = "hidden";

    /// <summary>What <see cref="HiddenKey"/> says when what is hidden is
    /// included; unsaid, it is left out.</summary>
    public const string Included = "include";

    /// <summary>The scope pattern, <c>*</c> and <c>?</c>, over each record's
    /// location; a record with none is at the root, which only <c>*</c>
    /// names.</summary>
    public string? Pattern { get; init; }

    /// <summary>Records whose process declared this scope or one beneath
    /// it.</summary>
    public string? Location { get; init; }

    /// <summary>Records of programs that declared no location, on this
    /// machine.</summary>
    public string? Host { get; init; }

    /// <summary>Records of this program, exactly.</summary>
    public string? Program { get; init; }

    /// <summary>The one record with this identifier; the other filters are
    /// set aside.</summary>
    public string? Record { get; init; }

    /// <summary>Information, Warning or Error.</summary>
    public string? Severity { get; init; }

    /// <summary>One action, exactly.</summary>
    public string? Action { get; init; }

    /// <summary>At or after: RFC 3339, or a date and time with no zone, read
    /// as UTC.</summary>
    public string? From { get; init; }

    /// <summary>At or before, as <see cref="From"/>.</summary>
    public string? To { get; init; }

    /// <summary>A column's word, as the read's
    /// <see cref="Xmip.Abi.Operate.AuditRead.Columns"/> lists them; null is
    /// <c>at</c>.</summary>
    public string? Sort { get; init; }

    /// <summary>ascending or descending; null is descending.</summary>
    public string? Order { get; init; }

    /// <summary>Where the page starts.</summary>
    public int Offset { get; init; }

    /// <summary>How long a page; 0 is the capability's default.</summary>
    public int Limit { get; init; }

    /// <summary>Whether the records of a run that declared itself hidden are
    /// read too — an assistant's test run (ADR-0028, amendment 2026-09-30);
    /// left out unless asked. Carried as <c>hidden=include</c>.</summary>
    public bool IncludeHidden { get; init; }

    /// <summary>Whether the audit chain of each writer of the records matched
    /// is walked whole and its verdict said (ADR-0070 clause 5): the verify
    /// act, which reads no payload. Carried as <c>verify=yes</c>.</summary>
    public bool Verify { get; init; }

    /// <summary>Whether the order is newest, or greatest, first.</summary>
    public bool Descending => !string.Equals(Order, "ascending", StringComparison.Ordinal);

    /// <summary>The query as the header carries it, key then value; what is
    /// not asked is not sent.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Pairs()
    {
        List<KeyValuePair<string, string>> pairs = [];

        foreach ((string key, string? value) in Said())
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                pairs.Add(new(key, value));
            }
        }

        return pairs;
    }

    /// <summary>Every word and its value, null where it is not asked, in the
    /// order the header lists them.</summary>
    public IEnumerable<(string Key, string? Value)> Said()
    {
        yield return ("pattern", Pattern);
        yield return ("location", Location);
        yield return ("host", Host);
        yield return ("program", Program);
        yield return ("record", Record);
        yield return ("severity", Severity);
        yield return ("action", Action);
        yield return ("from", From);
        yield return ("to", To);
        yield return ("sort", Sort);
        yield return ("order", Order);
        yield return ("offset", Offset > 0 ? $"{Offset}" : null);
        yield return ("limit", Limit > 0 ? $"{Limit}" : null);
        yield return (HiddenKey, IncludeHidden ? Included : null);
        yield return ("verify", Verify ? "yes" : null);
    }
}
