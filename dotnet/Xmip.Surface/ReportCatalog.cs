using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// Every Report, listed, and one opened, for every .NET surface (ADR-0072,
/// amendment 2026-11-10): <c>xmip report</c>, <c>Get-XmipReport</c> and the
/// GUI's Reports view. The list, each entry's kind, what each probe finds
/// and every refusal are the runtime's (<c>xmip_operate.h</c> section 25,
/// <see cref="RuntimeReports"/>), and the runtime's are
/// <c>xmip-core-report</c>'s; this asks and reads the answer into records.
/// A Quest's last probe is read back from where the surface's own audit
/// records go (<see cref="ProgramAudit"/>), and a probe is recorded there,
/// under the surface's program, by the runtime.
/// </summary>
public static class ReportCatalog
{
    /// <summary>Every Report, each Quest with its last probe as the audit
    /// where <paramref name="audit"/>'s records go holds it.</summary>
    /// <exception cref="InvalidOperationException">The runtime is not loaded,
    /// is a remote build, or answered with a refusal, in its words.</exception>
    public static ReportListing List(ProgramAudit audit) =>
        List(RuntimeLibrary.Rules.Reports, audit);

    /// <summary><see cref="List(ProgramAudit)"/> through
    /// <paramref name="reports"/>.</summary>
    public static ReportListing List(RuntimeReports reports, ProgramAudit audit)
    {
        return Asked(reports, audit, null);
    }

    /// <summary>The one Report named <paramref name="report"/>, exactly as
    /// the runtime opens it, listed alone with both kinds' words.</summary>
    /// <exception cref="InvalidOperationException">The runtime is not loaded,
    /// is a remote build, or refused, in its words: a name that is none is
    /// refused naming those there are.</exception>
    public static ReportListing Named(ProgramAudit audit, string report) =>
        Named(RuntimeLibrary.Rules.Reports, audit, report);

    /// <summary><see cref="Named(ProgramAudit, string)"/> through
    /// <paramref name="reports"/>.</summary>
    public static ReportListing Named(RuntimeReports reports, ProgramAudit audit, string report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return Asked(reports, audit, report);
    }

    private static ReportListing Asked(RuntimeReports reports, ProgramAudit audit, string? report)
    {
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentNullException.ThrowIfNull(audit);

        JsonObject arguments = new() { ["directory"] = audit.Directory ?? string.Empty };

        if (report is not null)
        {
            arguments["report"] = report;
        }

        return Listed(reports.Ask("list", arguments.ToJsonString()));
    }

    /// <summary>The Report <paramref name="probe"/> names opened: its Quest
    /// probes the configuration the probe gives and changes nothing, and the
    /// probe is audited under <paramref name="audit"/>'s program, where its
    /// records go.</summary>
    /// <exception cref="InvalidOperationException">The runtime is not
    /// loaded, is a remote build, or did not answer: its sentence, opening
    /// REFUSED or FAILED.</exception>
    public static ReportProbed Open(ReportProbe probe, ProgramAudit audit) =>
        Open(RuntimeLibrary.Rules.Reports, probe, audit);

    /// <summary><see cref="Open(ReportProbe, ProgramAudit)"/> through
    /// <paramref name="reports"/>.</summary>
    public static ReportProbed Open(RuntimeReports reports, ReportProbe probe, ProgramAudit audit)
    {
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(audit);

        JsonObject arguments = new()
        {
            ["report"] = probe.Report,
            ["configuration"] = probe.Configuration,
            ["program"] = audit.Program,
            ["directory"] = audit.Directory ?? string.Empty,
        };

        Given(arguments, "parameter", probe.Parameter);
        Given(arguments, "row", probe.Row);
        Given(arguments, "node", probe.Node);

        return Probed(reports.Ask("open", arguments.ToJsonString()));
    }

    /// <summary>The runtime's list, <paramref name="answer"/>, read.</summary>
    public static ReportListing Listed(string answer)
    {
        using JsonDocument document = JsonDocument.Parse(answer);
        JsonElement root = document.RootElement;
        ReportKindWords[] kinds = All(root.GetProperty("kinds").EnumerateArray().Select(
            kind => new ReportKindWords(
                (ReportKind)kind.GetProperty("kind").GetInt32(), Text(kind, "word"),
                Text(kind, "name"), Text(kind, "meaning"))));

        return new ReportListing(
            kinds,
            All(root.GetProperty("reports").EnumerateArray().Select(entry => Entry(entry, kinds))),
            Optional(root, "unread"),
            answer);
    }

    /// <summary>The runtime's answer to an open, <paramref name="answer"/>,
    /// read: what it holds, and nothing it does not.</summary>
    public static ReportProbed Probed(string answer)
    {
        using JsonDocument document = JsonDocument.Parse(answer);
        JsonElement root = document.RootElement;

        return new ReportProbed(
            Text(root, "report"),
            Optional(root, "parameter"),
            Text(root, "title"),
            Text(root, "node"),
            Text(root, "row"),
            Number(root, "took_ms") ?? 0,
            All(Each(root, "tally").Select(count => new ReportCount(
                Text(count, "state"), (int)(Number(count, "count") ?? 0)))),
            All(Each(root, "rows").Select(row => new ReportRow(
                Text(row, "name"), Text(row, "citation"), Text(row, "state")))),
            root.TryGetProperty("requirement", out JsonElement one) ? Requirement(one) : null,
            answer);
    }

    private static ReportEntry Entry(JsonElement entry, IReadOnlyList<ReportKindWords> kinds)
    {
        ReportKind kind = (ReportKind)entry.GetProperty("kind").GetInt32();
        ReportParameter? parameter = entry.TryGetProperty("parameter", out JsonElement taken)
            ? new ReportParameter(Text(taken, "name"), Strings(taken, "choices"))
            : null;

        return new ReportEntry(
            Text(entry, "name"),
            Text(entry, "covers"),
            kind,
            kinds.FirstOrDefault(words => words.Kind == kind)
                ?? throw new InvalidOperationException(
                    $"the runtime listed a Report of kind {(int)kind}, which it did not word"),
            entry.GetProperty("available").GetBoolean(),
            Optional(entry, "expect"),
            Optional(entry, "read_by"),
            parameter,
            Optional(entry, "row"),
            Strings(entry, "levels"),
            Number(entry, "took_ms"),
            Optional(entry, "probed_at") is { } at
                ? DateTimeOffset.Parse(at, CultureInfo.InvariantCulture)
                : null);
    }

    private static ReportRequirement Requirement(JsonElement one)
    {
        return new ReportRequirement(
            Text(one, "name"),
            Text(one, "citation"),
            Text(one, "state"),
            Text(one, "says"),
            All(Each(one, "behind").Select(behind => new ReportBehind(
                Text(behind, "kind"),
                All(behind.EnumerateObject()
                    .Where(field => field.Name != "kind")
                    .Select(field => KeyValuePair.Create(field.Name, field.Value.ToString())))))),
            All(Each(one, "actions").Select(action => new ReportAction(
                Text(action, "kind"), Optional(action, "setting"), Text(action, "says")))));
    }

    // Every one, as an array: a list PowerShell indexes, as a collection
    // expression's own read-only list is not.
    private static T[] All<T>(IEnumerable<T> each)
    {
        return [.. each];
    }

    // An optional member, there only where it was given.
    private static void Given(JsonObject arguments, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            arguments[name] = value;
        }
    }

    private static IEnumerable<JsonElement> Each(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement many)
            && many.ValueKind == JsonValueKind.Array
            ? many.EnumerateArray()
            : [];
    }

    private static string[] Strings(JsonElement element, string name)
    {
        return All(Each(element, name).Select(each => each.GetString() ?? string.Empty));
    }

    private static string Text(JsonElement element, string name)
    {
        return Optional(element, name) ?? string.Empty;
    }

    private static string? Optional(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static long? Number(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out long number)
            ? number
            : null;
    }
}

/// <summary>What a Report is opened for: the <paramref name="Report"/>
/// named, and the <paramref name="Configuration"/> its Quest probes, by
/// path — a node's configuration, or a cluster's <c>cluster.xmip</c> with
/// the <see cref="Node"/> it is sliced for, told apart by the runtime by what
/// the file holds.</summary>
public sealed record ReportProbe(string Report, string Configuration)
{
    /// <summary>The choice of the Report's parameter: <c>GDPR</c>.</summary>
    public string? Parameter { get; init; }

    /// <summary>The row of its second level to open in full: a
    /// requirement.</summary>
    public string? Row { get; init; }

    /// <summary>The node a cluster's <c>cluster.xmip</c> is sliced
    /// for.</summary>
    public string? Node { get; init; }
}
