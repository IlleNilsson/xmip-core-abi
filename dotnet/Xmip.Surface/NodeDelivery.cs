using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// What became of one node's slice when the cluster's <c>cluster.xmip</c> was
/// saved (ADR-0031, amendment 2026-10-05: <i>the cluster TOML file is
/// sliced into node TOML files and shipped to each node on save</i>): the
/// node, how it stands, and the sentence the page shows.
/// </summary>
/// <param name="Node">The node's key under <c>[nodes]</c>.</param>
/// <param name="Outcome">How it stands, and where its slice was written
/// when it was.</param>
/// <param name="Said">The sentence for a person: the runtime's refusal
/// where it refused.</param>
public sealed record NodeDelivery(string Node, NodeDelivery.Standing Outcome, string Said)
{
    /// <summary>The word the page and the audit say it by.</summary>
    public string Word => Outcome switch
    {
        Standing.Shipped => "shipped",
        Standing.NotShipped => "sliced, not shipped",
        Standing.Refused => "refused",
        null => throw UnionDefault.Of<Standing>(),
    };

    /// <summary>
    /// How a node's slice stands after a save, one of three. Xmip's own fixed
    /// answer, so a union: a switch over it names every case.
    /// </summary>
    public readonly union Standing(Standing.Shipped, Standing.NotShipped, Standing.Refused)
    {
        /// <summary>Sliced and placed where the node reads it.</summary>
        /// <param name="Path">The file its slice was written to.</param>
        public sealed record Shipped(string Path);

        /// <summary>Sliced and written, and not put on the node: no Xmip
        /// path reaches it from here.</summary>
        /// <param name="Path">The file its slice was written to.</param>
        public sealed record NotShipped(string Path);

        /// <summary>The runtime would not slice it, or it could not be
        /// written; nothing was.</summary>
        public sealed record Refused;
    }
}
