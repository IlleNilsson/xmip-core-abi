using System.Text.Json;

namespace Xmip.Surface;

/// <summary>
/// One node's configuration document, sliced from its cluster's
/// <c>xmip.toml</c> by the one slicing (<c>xmip_cluster_slices_v1</c>,
/// <c>configure::slice</c>; ADR-0031, amendments 2026-10-03 and
/// 2026-10-05). Nothing here takes the file apart.
/// </summary>
/// <param name="Node">The node's key under <c>[nodes]</c>.</param>
/// <param name="Text">Its document, as the slicing wrote it.</param>
public sealed record NodeSlice(string Node, string Text)
{
    /// <summary>Every node's slice of <paramref name="cluster"/>, or the one
    /// <paramref name="node"/> names. False, with the runtime's refusal in
    /// <paramref name="refusal"/>, for a node's own document, a node the
    /// cluster does not declare, or a node that does not slice.</summary>
    public static bool TrySlice(
        string cluster, string? node, out IReadOnlyList<NodeSlice> slices, out string refusal)
    {
        if (!RuntimeLibrary.Rules.Design.TrySlices(cluster, node, out string answer))
        {
            slices = [];
            refusal = answer;
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(answer);
        slices =
        [
            .. document.RootElement.GetProperty("slices").EnumerateArray().Select(slice =>
                new NodeSlice(
                    slice.GetProperty("node").GetString() ?? string.Empty,
                    slice.GetProperty("text").GetString() ?? string.Empty)),
        ];
        refusal = string.Empty;
        return true;
    }
}
