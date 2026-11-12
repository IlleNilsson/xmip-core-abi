using System.Text.Json;

namespace Xmip.Abi.Operate;

/// <summary>
/// One step of the path the work at a scope travels, as
/// <c>observe::topology::from_to</c> says it: what it is, its name, the node
/// it runs on — empty for a Party — and the transport it is reached over,
/// where the drawing says.
/// </summary>
/// <param name="Kind">What it is, in the words a person reads it by.</param>
/// <param name="Name">Its name.</param>
/// <param name="Node">The node it runs on; empty for a Party.</param>
/// <param name="Transport">What it is reached over; empty where unsaid.</param>
public sealed record Hop(string Kind, string Name, string Node, string Transport);

/// <summary>
/// Where the work observed at a scope came from and where it went, said by
/// the runtime once (<c>xmip_publication_from_to_v1</c>, section 8; the
/// owner, 2026-11-10: <i>from and to in every observation</i>). A surface
/// shows what is here, every word of it the runtime's, and writes none.
/// </summary>
/// <param name="From">Where it came from, in words.</param>
/// <param name="To">Where it went, in words.</param>
/// <param name="Hops">The path between them, in order.</param>
/// <param name="Unrouted">What a Dead Message Queue entry's to says: what no
/// Subscription took went nowhere.</param>
/// <param name="Line">The one line every surface says it in,
/// <c>from &lt;from&gt; → to &lt;to&gt;</c>; empty where nothing was
/// said.</param>
/// <param name="Path">The whole path, each hop's words in order.</param>
public sealed record FromTo(
    string From,
    string To,
    IReadOnlyList<Hop> Hops,
    string Unrouted,
    string Line,
    string Path)
{
    /// <summary>Nothing said: a scope on no node, or a drawing not made
    /// yet.</summary>
    public static FromTo None { get; } =
        new(string.Empty, string.Empty, [], string.Empty, string.Empty, string.Empty);

    /// <summary>Whether anything was said.</summary>
    public bool Said => Hops.Count > 0;

    /// <summary>What section 8 wrote.</summary>
    public static FromTo Parse(ReadOnlyMemory<byte> json)
    {
        using JsonDocument answer = JsonDocument.Parse(json);
        JsonElement root = answer.RootElement;

        static string Text(JsonElement entry, string name)
        {
            return entry.GetProperty(name).GetString() ?? string.Empty;
        }

        return new FromTo(
            Text(root, "from"),
            Text(root, "to"),
            [
                .. root.GetProperty("hops").EnumerateArray().Select(hop => new Hop(
                    Text(hop, "kind"), Text(hop, "name"), Text(hop, "node"),
                    Text(hop, "transport"))),
            ],
            Text(root, "unrouted"),
            Text(root, "line"),
            Text(root, "path"));
    }
}
