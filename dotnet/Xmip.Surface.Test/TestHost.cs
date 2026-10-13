namespace Xmip.Surface.Test;

/// <summary>
/// What the tests that stand in for a host share: a snapshot that takes acts,
/// and the TLS a test authority's files make.
/// </summary>
internal static class TestHost
{
    /// <summary>
    /// A snapshot of <paramref name="cluster"/> holding one Subscription on its
    /// first node and one Event subscription on its last, written in
    /// <paramref name="directory"/>, whose publisher takes orders in
    /// <paramref name="orders"/>: what the tests that act through the gate act on.
    /// </summary>
    public static SnapshotOperator Acting(TestCluster cluster, string directory, string orders)
    {
        ArgumentNullException.ThrowIfNull(cluster);

        string snapshot = Path.Combine(directory, "snapshot.toml");
        File.WriteAllText(
            snapshot,
            $"node = \"{cluster.Scope}\"\norders = '{orders}'\n"
            + $"[[subscriptions]]\nnode = \"{cluster.NodeScope(0)}\"\nname = \"routed\"\n"
            + "state = \"active\"\n"
            + $"[[event_subscriptions]]\nnode = \"{cluster.NodeScope(cluster.Nodes.Count - 1)}\"\n"
            + "id = 7\nsubscriber = \"p\"\nstate = \"active\"\n");

        return new SnapshotOperator(snapshot);
    }

    /// <summary>What a host or a surface presents and trusts: a pair the
    /// authority issued, and its anchor.</summary>
    public static SurfaceTls Tls(TestAuthority authority, (string Certificate, string Key) pair)
    {
        return SurfaceTls.Load(pair.Certificate, pair.Key, authority.Anchor);
    }
}
