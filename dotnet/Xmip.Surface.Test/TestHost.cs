namespace Xmip.Surface.Test;

/// <summary>
/// What the tests that stand in for a host share: a snapshot the gate's
/// tests act through, and the TLS a test authority's files make.
/// </summary>
internal static class TestHost
{
    /// <summary>
    /// A snapshot of <paramref name="cluster"/> holding one Subscription on its
    /// first node and one Event subscription on its last, written in
    /// <paramref name="directory"/>: what the tests that act through the gate
    /// act on. The snapshot itself declines every act it is passed.
    /// </summary>
    public static SnapshotOperator Acting(TestCluster cluster, string directory)
    {
        ArgumentNullException.ThrowIfNull(cluster);

        string snapshot = Path.Combine(directory, "snapshot.xmip");
        File.WriteAllText(
            snapshot,
            $"node = \"{cluster.Scope}\"\n"
            + $"[[subscriptions]]\nnode = \"{cluster.NodeScope(0)}\"\nname = \"routed\"\n"
            + "state = \"active\"\n"
            + $"[[event_subscriptions]]\nnode = \"{cluster.NodeScope(cluster.Nodes.Count - 1)}\"\n"
            + "id = 7\nsubscriber = \"p\"\nstate = \"active\"\n");

        return new SnapshotOperator(snapshot);
    }

    /// <summary>What a host or a surface presents and trusts: a pair the
    /// authority issued, and its anchor.</summary>
    public static SecureSurface Tls(TestAuthority authority, (string Certificate, string Key) pair)
    {
        return SecureSurface.Load(pair.Certificate, pair.Key, authority.Anchor);
    }
}
