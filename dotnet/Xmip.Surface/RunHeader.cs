using System.Globalization;
using Tomlyn.Model;

namespace Xmip.Surface;

/// <summary>
/// What a run was started with, as its publisher says it under <c>[run]</c>:
/// the cluster, the tests, the nodes, which of them are online, and how hard.
/// The owner, 2026-09-19: the run says what it was started with — a board
/// could not be told from the one before it. A publication with no such
/// table is <see cref="None"/>, and a surface shows nothing for it.
/// </summary>
public sealed record RunHeader(
    string Cluster,
    IReadOnlyList<string> Tests,
    IReadOnlyList<string> Nodes,
    IReadOnlyList<string> Online,
    string Stress)
{
    /// <summary>What a publication that says nothing of its run is read as.</summary>
    public static RunHeader None { get; } = new(string.Empty, [], [], [], string.Empty);

    /// <summary>Whether the publisher said anything.</summary>
    public bool Said =>
        Cluster.Length > 0 || Tests.Count > 0 || Nodes.Count > 0 || Stress.Length > 0;

    /// <summary>
    /// The one line every surface shows:
    /// <c>RoundTrip · C1 · nodes R1 P1 S1 · online R1 · realistic</c>. A part
    /// the publisher left out is left out; no nodes and none online are said
    /// in words, because both are choices.
    /// </summary>
    public string Line()
    {
        if (!Said)
        {
            return string.Empty;
        }

        List<string> parts = [];

        if (Tests.Count > 0)
        {
            parts.Add(string.Join(' ', Tests));
        }

        if (Cluster.Length > 0)
        {
            parts.Add(Cluster);
        }

        parts.Add(Nodes.Count > 0 ? $"nodes {string.Join(' ', Nodes)}" : "no nodes");

        if (Nodes.Count > 0)
        {
            parts.Add(Online.Count > 0 ? $"online {string.Join(' ', Online)}" : "none online");
        }

        if (Stress.Length > 0)
        {
            parts.Add(Stress);
        }

        return string.Join(" · ", parts);
    }

    /// <summary>The header of a snapshot document, or <see cref="None"/> when
    /// it carries no <c>[run]</c> table.</summary>
    public static RunHeader Read(TomlTable document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document.TryGetValue("run", out object? found) && found is TomlTable run
            ? new RunHeader(
                Word(run, "cluster"),
                Words(run, "tests"),
                Words(run, "nodes"),
                Words(run, "online"),
                Word(run, "stress"))
            : None;
    }

    private static string Word(TomlTable table, string key)
    {
        return table.TryGetValue(key, out object? found)
            ? Convert.ToString(found, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;
    }

    private static List<string> Words(TomlTable table, string key)
    {
        return table.TryGetValue(key, out object? found) && found is TomlArray words
            ? [.. words.OfType<string>()]
            : [];
    }
}
