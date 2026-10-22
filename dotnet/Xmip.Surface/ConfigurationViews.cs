using System.Text.Json;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// A cluster's <c>xmip.toml</c> as the runtime views it, artifact kind by
/// artifact kind (<c>xmip_operate.h</c> section 10,
/// <c>xmip_cluster_views_v1</c>; ADR-0064, amendment 2026-10-03): what the
/// VS Code designer draws and the Operation Desktop's Configure page draws.
/// Every kind, title, entry, field and sentence is the runtime's answer read
/// as it came; nothing here decides what the file holds (ADR-0031,
/// amendment 2026-10-05).
/// </summary>
/// <param name="Cluster">The cluster's <c>[service] cluster_name</c>; empty
/// when it names none.</param>
/// <param name="IsCluster">Whether the text is a cluster's file, declaring
/// its nodes — the runtime's <c>document</c>; a node's own document is
/// never edited.</param>
/// <param name="Views">One per artifact kind, in the order the runtime
/// lists them.</param>
public sealed record ConfigurationViews(
    string Cluster, bool IsCluster, IReadOnlyList<ArtifactView> Views)
{
    /// <summary>The view of <paramref name="kind"/>, or null where the runtime
    /// answers none.</summary>
    public ArtifactView? Of(string kind)
    {
        return Views.FirstOrDefault(view => view.Kind == kind);
    }

    /// <summary>Read the runtime's views of <paramref name="text"/>. False,
    /// with the reader's sentence in <paramref name="refusal"/>, when the
    /// text is not TOML.</summary>
    public static bool TryRead(string text, out ConfigurationViews views, out string refusal)
    {
        return TryRead(RuntimeLibrary.Rules.Design, text, out views, out refusal);
    }

    /// <summary>As <see cref="TryRead(string, out ConfigurationViews, out string)"/>,
    /// through <paramref name="design"/>.</summary>
    public static bool TryRead(
        RuntimeDesign design, string text, out ConfigurationViews views, out string refusal)
    {
        ArgumentNullException.ThrowIfNull(design);

        if (!design.TryViews(text, out string answer))
        {
            views = new ConfigurationViews(string.Empty, false, []);
            refusal = answer;
            return false;
        }

        views = Parse(answer);
        refusal = string.Empty;
        return true;
    }

    /// <summary>The header's JSON, read.</summary>
    public static ConfigurationViews Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        return new ConfigurationViews(
            Text(root, "cluster"),
            Text(root, "document") == "cluster",
            [.. Items(root, "views").Select(View)]);
    }

    private static ArtifactView View(JsonElement view)
    {
        return new ArtifactView(
            Text(view, "kind"),
            Text(view, "title"),
            view.TryGetProperty("note", out JsonElement note) ? note.GetString() : null,
            [.. Items(view, "entries").Select(Entry)],
            [.. Items(view, "places").Select(place =>
                new ArtifactPlace(Words(place, "section"), Text(place, "scope")))]);
    }

    private static ArtifactEntry Entry(JsonElement entry)
    {
        return new ArtifactEntry(
            Words(entry, "section"),
            Text(entry, "name"),
            Text(entry, "scope"),
            [.. Items(entry, "fields").Select(field =>
                new ArtifactField(Words(field, "key"), Text(field, "value"), Text(field, "kind")))],
            [.. Items(entry, "problems").Select(problem => problem.GetString() ?? string.Empty)],
            [.. Items(entry, "offered").Select(Offer)]);
    }

    private static OfferedKey Offer(JsonElement offered)
    {
        return new OfferedKey(
            Text(offered, "key"),
            Text(offered, "kind"),
            offered.TryGetProperty("required", out JsonElement required) && required.GetBoolean(),
            Words(offered, "choices"),
            [.. Items(offered, "elements").Select(Offer)]);
    }

    private static JsonElement[] Items(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement items)
            && items.ValueKind == JsonValueKind.Array
                ? [.. items.EnumerateArray()]
                : [];
    }

    private static string[] Words(JsonElement element, string name)
    {
        return [.. Items(element, name).Select(word => word.GetString() ?? string.Empty)];
    }

    private static string Text(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }
}

/// <summary>One artifact kind and the entries the file holds of it.</summary>
/// <param name="Kind">The runtime's word: <c>cluster</c>, <c>node</c>,
/// <c>receive-port</c>, … <c>process</c>.</param>
/// <param name="Title">As a person reads it.</param>
/// <param name="Note">What the runtime says of the kind, if anything.</param>
/// <param name="Entries">The tables the file holds of it.</param>
/// <param name="Places">Where a new entry of it may be added.</param>
public sealed record ArtifactView(
    string Kind,
    string Title,
    string? Note,
    IReadOnlyList<ArtifactEntry> Entries,
    IReadOnlyList<ArtifactPlace> Places);

/// <summary>One table of the file.</summary>
/// <param name="Section">The path an edit names it by: keys, and an entry of
/// a list by its name.</param>
/// <param name="Name">Its name.</param>
/// <param name="Scope">Whose it is: <c>cluster</c>, a node's name, a
/// binding's or an Xmip Application's.</param>
/// <param name="Fields">Its values as the file writes them.</param>
/// <param name="Problems">Why a Route entry does not read as an Xmip
/// Application; empty otherwise.</param>
/// <param name="Offered">What may be set on it, as the runtime offers it
/// (ADR-0064, amendment 2026-10-22).</param>
public sealed record ArtifactEntry(
    IReadOnlyList<string> Section,
    string Name,
    string Scope,
    IReadOnlyList<ArtifactField> Fields,
    IReadOnlyList<string> Problems,
    IReadOnlyList<OfferedKey> Offered);

/// <summary>A key the runtime offers to set on an entry.</summary>
/// <param name="Key">The key.</param>
/// <param name="Kind">Its kind, in the runtime's word.</param>
/// <param name="Required">Whether the entry is refused without it.</param>
/// <param name="Choices">The words or names its value may take; empty where
/// any value of its kind may.</param>
/// <param name="Elements">For an array of unnamed tables, such as a Party's
/// identities: the keys one element holds.</param>
public sealed record OfferedKey(
    string Key,
    string Kind,
    bool Required,
    IReadOnlyList<string> Choices,
    IReadOnlyList<OfferedKey> Elements);

/// <summary>One value of an entry.</summary>
/// <param name="Key">Its key within the entry: one key, or a sub-table's
/// path to it.</param>
/// <param name="Value">The value in TOML, as the file writes it.</param>
/// <param name="Kind">The runtime's word: <c>text</c>, <c>integer</c>,
/// <c>float</c>, <c>boolean</c>, <c>date</c> or <c>array</c>.</param>
public sealed record ArtifactField(IReadOnlyList<string> Key, string Value, string Kind);

/// <summary>A list a new entry may be added to.</summary>
/// <param name="Section">The list's path, its last key the list.</param>
/// <param name="Scope">Whose list it is.</param>
public sealed record ArtifactPlace(IReadOnlyList<string> Section, string Scope);
