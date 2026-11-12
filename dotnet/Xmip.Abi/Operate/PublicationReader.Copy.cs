using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

// A publication copied whole out of the runtime's handle, before it is
// freed.
public sealed partial class PublicationReader
{
    private Publication Copied(nint handle)
    {
        XmipPublicationHead head;

        // Everything the handle lends is copied out here, before Read frees it.
        unsafe
        {
            Check(_head(handle, &head));

            string source = head.Source.Read();

            return new Publication(
                source,
                head.Node.Read(),
                [.. Fill(_records, handle).Select(PublicationEntries.Record)],
                [.. Fill(_counts, handle).Select(PublicationEntries.Count)],
                head.HasTopology == 0 ? null : Topology(handle, head, source),
                head.HasRun == 0 ? null : Run(handle, head),
                SubscriptionList.Parse(Listed(_subscriptions, handle)),
                EventSubscriptionList.Parse(Listed(_eventSubscriptions, handle)),
                DeadMessageList.Parse(Listed(_deadMessages, handle)),
                FailedMessageList.Parse(Listed(_failedMessages, handle)));
        }
    }

    // Section 11's, 14's, 15's or 16's list over this handle: JSON, asked again at its length.
    private static ReadOnlyMemory<byte> Listed(
        delegate* unmanaged[Cdecl]<nint, byte*, nuint, nuint*, int> list, nint handle)
    {
        byte[] text = new byte[4096];
        nuint needed = 0;

        fixed (byte* into = text)
        {
            unsafe
            {
                Check(list(handle, into, (nuint)text.Length, &needed));
            }
        }

        if (needed > (nuint)text.Length)
        {
            text = new byte[checked((int)needed)];

            fixed (byte* into = text)
            {
                unsafe
                {
                    Check(list(handle, into, (nuint)text.Length, &needed));
                }
            }
        }

        return text.AsMemory(0, (int)needed);
    }

    private TopologySnapshot Topology(nint handle, XmipPublicationHead head, string source)
    {
        unsafe
        {
            string drawnBy = head.TopologySource.Read();
            TopologyNode[] nodes = [.. Fill(_nodes, handle).Select(PublicationEntries.Node)];

            return new TopologySnapshot(
                nodes,
                [.. Fill(_links, handle).Select(PublicationEntries.Link)],
                Operator.FromNanos(head.TopologyObservedUnixNanos),
                drawnBy.Length > 0 ? drawnBy : source,
                Paths(handle, nodes));
        }
    }

    private PublishedRun Run(nint handle, XmipPublicationHead head)
    {
        unsafe
        {
            return new PublishedRun(
                head.Cluster.Read(),
                Listed(handle, RunList.Tests),
                Listed(handle, RunList.Nodes),
                Listed(handle, RunList.Roles),
                Listed(handle, RunList.Online),
                head.Stress.Read(),
                head.Hidden != 0,
                Listed(handle, RunList.Real));
        }
    }

    private string[] Listed(nint handle, RunList list)
    {
        nuint count = 0;

        unsafe
        {
            Check(_run(handle, (uint)list, null, 0, &count));
        }

        XmipStr[] words = new XmipStr[count];

        fixed (XmipStr* into = words)
        {
            unsafe
            {
                Check(_run(handle, (uint)list, into, count, &count));
            }
        }

        unsafe
        {
            return [.. words.Select(word => word.Read())];
        }
    }

    private static T[] Fill<T>(
        delegate* unmanaged[Cdecl]<nint, T*, nuint, nuint*, int> fill, nint handle)
        where T : unmanaged
    {
        nuint count = 0;

        unsafe
        {
            Check(fill(handle, null, 0, &count));
        }

        T[] found = new T[count];

        fixed (T* into = found)
        {
            unsafe
            {
                Check(fill(handle, into, count, &count));
            }
        }

        return found;
    }

    // Any other status is a runtime breaking section 8's promise, not an answer.
    private static void Check(int status)
    {
        if (status != 0)
        {
            throw new InvalidOperationException(
                $"the runtime answered a section 8 read with {(XmipStatus)status}");
        }
    }
}
