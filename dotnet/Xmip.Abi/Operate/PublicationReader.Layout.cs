using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

// Where each thing a publication draws stands on the map, asked of the
// runtime while its handle is open (section 8, xmip_publication_layout_v1):
// the one rule is observe's, by the roles a node declares and the order of
// [storage] nodes, and nothing here works a place out (ADR-0052 and
// ADR-0056, amendments 2026-11-13).
public sealed partial class PublicationReader
{
    // The places beneath every focus that has something beneath it, by the
    // focus's id: the top, the empty string, and every parent a node names
    // (TopologySnapshot.Places). Nothing is filtered: a surface shows what
    // the runtime laid out beneath the focus it is open at.
    private Dictionary<string, IReadOnlyList<TopologyPlace>> Places(
        nint handle, IReadOnlyList<TopologyNode> nodes)
    {
        Dictionary<string, IReadOnlyList<TopologyPlace>> places = [with(StringComparer.Ordinal)];
        IEnumerable<string> foci = nodes
            .Select(node => node.ParentId ?? string.Empty)
            .Prepend(string.Empty)
            .Distinct(StringComparer.Ordinal);

        foreach (string focus in foci)
        {
            int beneath = nodes.Count(node => (node.ParentId ?? string.Empty) == focus);

            places[focus] = Laid(handle, focus, beneath);
        }

        return places;
    }

    // One focus's places, room made for what is beneath it and asked again
    // should the runtime lay out more.
    private TopologyPlace[] Laid(nint handle, string focus, int beneath)
    {
        using PinnedStr pinned = new(focus);
        XmipTopologyPlace[] found = new XmipTopologyPlace[beneath];
        nuint count = 0;

        unsafe
        {
            XmipStr asked = pinned.Value;

            fixed (XmipTopologyPlace* into = found)
            {
                Check(_layout(handle, asked, null, 0, into, (nuint)found.Length, &count));
            }

            if (count > (nuint)found.Length)
            {
                found = new XmipTopologyPlace[count];

                fixed (XmipTopologyPlace* into = found)
                {
                    Check(_layout(handle, asked, null, 0, into, (nuint)found.Length, &count));
                }
            }

            return [.. found.Take(checked((int)count)).Select(PublicationEntries.Place)];
        }
    }
}
