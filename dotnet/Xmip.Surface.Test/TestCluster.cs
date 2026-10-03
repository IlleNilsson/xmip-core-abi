using Microsoft.Extensions.Configuration;

namespace Xmip.Surface.Test;

/// <summary>
/// The test cluster, the one place a .NET test takes a cluster's or a node's
/// name from (the owner, 2026-10-03: names in code and tests are parameters
/// and configuration, never literals; ADR-0056, amendment 2026-10-03). It
/// reads what the Rust tests read through <c>configure::fixture</c>: the
/// file <see cref="Variable"/> names, else the estate's
/// <see cref="EstateFile"/>. One file, compiled into every .NET test project
/// that names a cluster or a node (each links it), so there is one reader.
/// </summary>
internal sealed class TestCluster
{
    /// <summary>The variable naming the test cluster's file.</summary>
    public const string Variable = "XMIP_TEST_CLUSTER";

    /// <summary>Where the estate keeps its test cluster's file, from the
    /// estate's root.</summary>
    public const string EstateFile = "test/xmip.toml";

    /// <summary>Where the estate keeps its second test cluster's file, the
    /// cluster a test of two takes beside the test cluster, as
    /// <c>configure::fixture::other_cluster</c> reads it.</summary>
    public const string OtherFile = "test/other/xmip.toml";

    private TestCluster(
        string path, string name, IReadOnlyList<string> nodes, IReadOnlyList<string> roles)
    {
        Path = path;
        Name = name;
        Nodes = nodes;
        Roles = roles;
    }

    /// <summary>Where it was read from.</summary>
    public string Path { get; }

    /// <summary>Its <c>[service] cluster_name</c>.</summary>
    public string Name { get; }

    /// <summary>Its nodes' names, in ordinal order.</summary>
    public IReadOnlyList<string> Nodes { get; }

    /// <summary>What each node declares, its <c>roles</c>, in the order of
    /// <see cref="Nodes"/>.</summary>
    public IReadOnlyList<string> Roles { get; }

    /// <summary>The cluster's scope.</summary>
    public string Scope => $"{ScopeTree.Root}{Name}";

    /// <summary>The scope of the node at <paramref name="place"/>.</summary>
    public string NodeScope(int place)
    {
        return $"{Scope}/node/{Nodes[place]}";
    }

    /// <summary>The first node, in <see cref="Nodes"/>' order, that declares
    /// <paramref name="role"/>: a test finds a node by what it declares.</summary>
    /// <exception cref="InvalidOperationException">No node declares it.</exception>
    public string WithRole(string role)
    {
        for (int place = 0; place < Nodes.Count; place++)
        {
            if (Roles[place].Split(',', '+').Select(word => word.Trim()).Contains(role))
            {
                return Nodes[place];
            }
        }

        throw new InvalidOperationException($"no node of the test cluster {Path} declares {role}");
    }

    /// <summary>The test cluster: the file <see cref="Variable"/> names, else
    /// the estate's.</summary>
    /// <exception cref="InvalidOperationException">Neither is there, or it
    /// names no cluster or no node: a test cannot run without it.</exception>
    public static TestCluster Read()
    {
        return Read(Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } stated
            ? stated
            : Estate(EstateFile));
    }

    /// <summary>The second test cluster, the estate's <see cref="OtherFile"/>.</summary>
    /// <exception cref="InvalidOperationException">As <see cref="Read()"/>, and
    /// where it names the cluster <see cref="Read()"/> does.</exception>
    public static TestCluster ReadOther()
    {
        TestCluster other = Read(Estate(OtherFile));

        return other.Name == Read().Name
            ? throw new InvalidOperationException(
                $"the test clusters {other.Path} and {Read().Path} name one cluster")
            : other;
    }

    private static string Estate(string file)
    {
        return System.IO.Path.Combine(TomlDocument.BasePath(AppContext.BaseDirectory), file);
    }

    private static TestCluster Read(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"the test cluster {path} is not there");
        }

        IConfigurationRoot document = TomlDocument.Read(path);
        string name = document["service:cluster_name"]
            ?? throw new InvalidOperationException(
                $"the test cluster {path} names no cluster_name");
        IConfigurationSection[] nodes =
        [
            .. document.GetSection("nodes").GetChildren()
                .OrderBy(node => node.Key, StringComparer.Ordinal),
        ];

        return nodes.Length == 0
            ? throw new InvalidOperationException($"the test cluster {path} declares no node")
            : new TestCluster(
                path,
                name,
                [.. nodes.Select(node => node.Key)],
                [.. nodes.Select(node => node["roles"] ?? string.Empty)]);
    }
}
