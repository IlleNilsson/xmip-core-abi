using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Xmip.Surface;

/// <summary>
/// Where the desktop's cluster lives and where its nodes' slices go: the
/// cluster's <c>cluster.xmip</c>, the directory each node's slice is written
/// to as <c>&lt;node&gt;/node.xmip</c> — the name desired state writes
/// it by (<c>deployment-model.md</c> section 8) — and the node of it this
/// desktop starts, as <c>gui.xmip</c> declares them. Every slice is the
/// runtime's one slicing, signed by the cluster's authority beside the
/// cluster's file (<c>xmip_cluster_signed_slice_v1</c>; ADR-0031, amendment
/// 2026-11-06), and written with that authority's certificate beside it;
/// nothing here takes the file apart or signs it.
/// </summary>
/// <remarks>
/// Shipping puts a slice where its node reads it. The node this desktop
/// starts reads its slice here, so writing it is shipping it. Xmip has no
/// path yet that puts a file on another node: desired state slices the
/// cluster's file on each node as it deploys it (the Ansible role
/// <c>xmip_node</c>), and the node's
/// operate listener (ADR-0067) takes no configuration. Another node's slice
/// is written here and said to be not shipped, never sent by a transport
/// invented for it.
/// </remarks>
/// <param name="clusterPath">The cluster's <c>cluster.xmip</c>.</param>
/// <param name="directory">Where each node's slice is written.</param>
/// <param name="node">The node this desktop starts; null where it starts
/// none.</param>
public sealed class SliceDelivery(string clusterPath, string directory, string? node)
{
    /// <summary>The file name a node reads its slice from.</summary>
    public const string FileName = "node.xmip";

    /// <summary>The folder beside a slice holding the certificate of the
    /// cluster's authority that signed it, which the node checks it by
    /// (<c>signed::AUTHORITY</c>).</summary>
    public const string AuthorityFolder = "authority";

    /// <summary>That certificate's file (<c>provision::issuer::CERTIFICATE</c>).</summary>
    public const string AuthorityCertificate = "authority.pem";

    /// <summary>The key naming the cluster's <c>cluster.xmip</c>.</summary>
    public const string ClusterKey = "Xmip:ClusterConfiguration";

    /// <summary>The key naming where each node's slice is written.</summary>
    public const string SliceDirectoryKey = "Xmip:SliceDirectory";

    /// <summary>The key naming the node configured here.</summary>
    public const string NodeKey = "Xmip:Node";

    /// <summary>
    /// The place <paramref name="configuration"/> declares — the cluster's
    /// file, the slice directory and the node configured here, all three as
    /// <c>gui.xmip</c> names them and never read from a name. A path is
    /// resolved against <paramref name="basePath"/> (<see cref="TomlDocument.Resolve"/>);
    /// one unset is <c>cluster.xmip</c> or <c>slices</c> under
    /// <paramref name="data"/>, the host's own data directory; no node set
    /// is none.
    /// </summary>
    public static SliceDelivery From(IConfiguration configuration, string basePath, string data)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? cluster = configuration[ClusterKey];
        string? slices = configuration[SliceDirectoryKey];
        string? node = configuration[NodeKey];

        return new SliceDelivery(
            string.IsNullOrWhiteSpace(cluster)
                ? Path.Combine(data, "cluster.xmip")
                : TomlDocument.Resolve(cluster, basePath),
            string.IsNullOrWhiteSpace(slices)
                ? Path.Combine(data, "slices")
                : TomlDocument.Resolve(slices, basePath),
            string.IsNullOrWhiteSpace(node) ? null : node);
    }

    /// <summary>The cluster's <c>cluster.xmip</c>.</summary>
    public string ClusterPath => clusterPath;

    /// <summary>Where each node's slice is written.</summary>
    public string Directory => directory;

    /// <summary>The node this desktop starts; null where it starts none.</summary>
    public string? Node => node;

    /// <summary>Where <paramref name="name"/>'s slice is written.</summary>
    public string PathOf(string name)
    {
        return Path.Combine(directory, name, FileName);
    }

    /// <summary>Slice <paramref name="cluster"/> for every node it declares
    /// and put each slice where it can go, node by node: one node's refusal
    /// stops no other's. Nothing is delivered when the file on disk no longer
    /// holds exactly the cluster's text — a newer save replaced it, and its
    /// slices are the ones that stand; each node then says so. A save calls
    /// this while it holds the file (<see cref="ClusterConfiguration.TryWrite"/>),
    /// so no other Xmip save delivers in between.</summary>
    public IReadOnlyList<NodeDelivery> Deliver(ClusterConfiguration cluster)
    {
        ArgumentNullException.ThrowIfNull(cluster);

        if (!cluster.OnDisk())
        {
            string file = Path.GetFileName(cluster.Path);
            return (NodeDelivery[])[.. cluster.Nodes.Select(name => new NodeDelivery(
                name, new NodeDelivery.Standing.Refused(),
                $"REFUSED: {file} on disk is no longer the text this save wrote; a newer save "
                + "replaced it, and its slice stands. Nothing was delivered."))];
        }

        return (NodeDelivery[])[.. cluster.Nodes.Select(name => Deliver(cluster.Text, name))];
    }

    /// <summary>Slice <paramref name="text"/>, a cluster's file, for
    /// <paramref name="name"/> and put the slice where it can go.</summary>
    public NodeDelivery Deliver(string text, string name)
    {
        if (!RuntimeLibrary.Rules.Design.TrySignedSlice(text, clusterPath, name, out string answer))
        {
            return new NodeDelivery(name, new NodeDelivery.Standing.Refused(), answer);
        }

        using JsonDocument signed = JsonDocument.Parse(answer);
        string slice = signed.RootElement.GetProperty("text").GetString() ?? string.Empty;
        string anchor = signed.RootElement.GetProperty("authority").GetString() ?? string.Empty;
        string path = PathOf(name);
        string folder = Path.Combine(Path.GetDirectoryName(path)!, AuthorityFolder);
        UTF8Encoding utf8 = new(false);

        try
        {
            System.IO.Directory.CreateDirectory(folder);
            WholeFile.Replace(Path.Combine(folder, AuthorityCertificate), utf8.GetBytes(anchor));
            WholeFile.Replace(path, utf8.GetBytes(slice));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new NodeDelivery(
                name, new NodeDelivery.Standing.Refused(),
                $"REFUSED: sliced, and not written to {path}: {exception.Message}");
        }

        return name == node
            ? new NodeDelivery(
                name, new NodeDelivery.Standing.Shipped(path),
                $"shipped to {path}, where this desktop starts it; it takes effect when the " +
                "node starts again")
            : new NodeDelivery(
                name, new NodeDelivery.Standing.NotShipped(path),
                $"sliced to {path} and not shipped: Xmip has no path yet that puts a file on " +
                "another node; desired state slices cluster.xmip on the node as it " +
                "deploys it");
    }
}
