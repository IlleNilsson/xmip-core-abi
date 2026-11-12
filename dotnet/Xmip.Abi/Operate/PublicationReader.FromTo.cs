using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

// From and to of everything a publication draws, asked of the runtime while
// its handle is open (section 8, xmip_publication_from_to_v1): the one rule
// is observe's, and nothing here works a path out.
public sealed partial class PublicationReader
{
    // A thing's from and to, by the scope it is drawn at: a node, a stage,
    // a Receive Location, a Subscription, a Send Port. A scope beneath one of
    // them has its from and to (TopologySnapshot.Paths).
    private Dictionary<string, FromTo> Paths(nint handle, IEnumerable<TopologyNode> nodes)
    {
        Dictionary<string, FromTo> paths = [with(StringComparer.Ordinal)];

        foreach (string scope in nodes
            .Select(node => node.Scope)
            .Where(scope => scope.Length > 0)
            .Distinct(StringComparer.Ordinal))
        {
            FromTo said = Said(handle, scope);

            if (said.Hops.Count > 0)
            {
                paths[scope] = said;
            }
        }

        return paths;
    }

    // One scope's from and to: JSON, asked again at its length.
    private FromTo Said(nint handle, string scope)
    {
        using PinnedStr pinned = new(scope);
        byte[] text = new byte[2048];
        nuint needed = 0;

        unsafe
        {
            XmipStr asked = pinned.Value;

            fixed (byte* into = text)
            {
                Check(_fromTo(handle, asked, into, (nuint)text.Length, &needed));
            }

            if (needed > (nuint)text.Length)
            {
                text = new byte[checked((int)needed)];

                fixed (byte* into = text)
                {
                    Check(_fromTo(handle, asked, into, (nuint)text.Length, &needed));
                }
            }
        }

        return FromTo.Parse(text.AsMemory(0, (int)needed));
    }
}
