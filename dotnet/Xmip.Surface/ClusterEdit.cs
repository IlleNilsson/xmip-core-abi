using System.Text.Json;

namespace Xmip.Surface;

/// <summary>
/// One act on a cluster's <c>xmip.toml</c>, as <c>xmip_cluster_edit_v1</c>
/// takes it (<c>xmip_operate.h</c> section 10; <c>configure::view_edit</c>):
/// the header's JSON, written once here for every .NET surface. Whether the
/// edit is good, and the text it leaves, are the runtime's answer — a value
/// is TOML as the file writes it, and an edit that would leave a node unable
/// to read its slice is refused in the runtime's words.
/// </summary>
public abstract record ClusterEdit
{
    /// <summary>The edit as the header's JSON.</summary>
    public abstract string ToJson();

    /// <summary>Make the edit to <paramref name="cluster"/> through the
    /// runtime. True with the edited text in <paramref name="answer"/>; false
    /// with the refusal, one sentence.</summary>
    public bool TryApply(string cluster, out string answer)
    {
        return RuntimeLibrary.Rules.Design.TryEdit(cluster, ToJson(), out answer);
    }

    /// <summary>Write <paramref name="Value"/>, TOML, at
    /// <paramref name="Key"/> in the table at <paramref name="Section"/>.</summary>
    public sealed record Set(
        IReadOnlyList<string> Section, IReadOnlyList<string> Key, string Value) : ClusterEdit
    {
        /// <inheritdoc />
        public override string ToJson()
        {
            return Json("set", new { section = Section, key = Key, value = Value });
        }
    }

    /// <summary>Remove the value at <paramref name="Key"/> in the table at
    /// <paramref name="Section"/>.</summary>
    public sealed record Remove(IReadOnlyList<string> Section, IReadOnlyList<string> Key)
        : ClusterEdit
    {
        /// <inheritdoc />
        public override string ToJson()
        {
            return Json("remove", new { section = Section, key = Key });
        }
    }

    /// <summary>Add an entry named <paramref name="Name"/> to the list at
    /// <paramref name="Section"/>, its last key the list, with
    /// <paramref name="Values"/>: what the entry must hold to read, which the
    /// runtime names where it is missing.</summary>
    public sealed record AddEntry(
        IReadOnlyList<string> Section, string Name, IReadOnlyList<ArtifactField>? Values = null)
        : ClusterEdit
    {
        /// <inheritdoc />
        public override string ToJson()
        {
            return Json("add-entry", new
            {
                section = Section,
                name = Name,
                values = (Values ?? []).Select(given =>
                    new { key = given.Key, value = given.Value }),
            });
        }
    }

    /// <summary>Remove the table at <paramref name="Section"/>: an entry of a
    /// list, or a node.</summary>
    public sealed record RemoveEntry(IReadOnlyList<string> Section) : ClusterEdit
    {
        /// <inheritdoc />
        public override string ToJson()
        {
            return Json("remove-entry", new { section = Section });
        }
    }

    /// <summary>Declare a node, <c>[nodes.&lt;name&gt;]</c>.</summary>
    public sealed record AddNode(string Name) : ClusterEdit
    {
        /// <inheritdoc />
        public override string ToJson()
        {
            return Json("add-node", new { name = Name });
        }
    }

    private static string Json(string act, object body)
    {
        return JsonSerializer.Serialize(new Dictionary<string, object> { [act] = body });
    }
}
