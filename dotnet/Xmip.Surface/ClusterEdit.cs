using System.Text.Json;

namespace Xmip.Surface;

/// <summary>
/// One act on a cluster's <c>cluster.xmip</c>, as <c>xmip_cluster_edit_v1</c>
/// takes it (<c>xmip_operate.h</c> section 10; <c>configure::view_edit</c>):
/// the header's JSON, written once here for every .NET surface. Whether the
/// edit is good, and the text it leaves, are the runtime's answer — a value
/// is TOML as the file writes it, and an edit that would leave a node unable
/// to read its slice is refused in the runtime's words. The header's acts
/// are Xmip's own and fixed, so the hierarchy is closed: no assembly adds
/// one, and a switch over it names every act.
/// </summary>
public closed record ClusterEdit
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
            return Json("set", writer =>
            {
                Strings(writer, "section", Section);
                Strings(writer, "key", Key);
                writer.WriteString("value", Value);
            });
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
            return Json("remove", writer =>
            {
                Strings(writer, "section", Section);
                Strings(writer, "key", Key);
            });
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
            return Json("add-entry", writer =>
            {
                Strings(writer, "section", Section);
                writer.WriteString("name", Name);
                Fields(writer, Values ?? []);
            });
        }
    }

    /// <summary>Append to the array at <paramref name="Key"/> of the table at
    /// <paramref name="Section"/> one element of <paramref name="Values"/>,
    /// each TOML: a Party's identity, say (ADR-0064, amendment
    /// 2026-10-22).</summary>
    public sealed record AddElement(
        IReadOnlyList<string> Section, IReadOnlyList<string> Key,
        IReadOnlyList<ArtifactField> Values) : ClusterEdit
    {
        /// <inheritdoc />
        public override string ToJson()
        {
            return Json("add-element", writer =>
            {
                Strings(writer, "section", Section);
                Strings(writer, "key", Key);
                Fields(writer, Values);
            });
        }
    }

    /// <summary>Remove the table at <paramref name="Section"/>: an entry of a
    /// list, or a node. Refused while anything in the file names it, saying
    /// what does, unless <paramref name="References"/>, which removes what
    /// names it with it (ADR-0064, amendment 2026-10-13).</summary>
    public sealed record RemoveEntry(IReadOnlyList<string> Section, bool References = false)
        : ClusterEdit
    {
        /// <inheritdoc />
        public override string ToJson()
        {
            return Json("remove-entry", writer =>
            {
                Strings(writer, "section", Section);
                writer.WriteBoolean("references", References);
            });
        }
    }

    /// <summary>Rename the entry or node at <paramref name="Section"/> to
    /// <paramref name="Name"/>, and every name in the file that stands for
    /// it.</summary>
    public sealed record Rename(IReadOnlyList<string> Section, string Name) : ClusterEdit
    {
        /// <inheritdoc />
        public override string ToJson()
        {
            return Json("rename", writer =>
            {
                Strings(writer, "section", Section);
                writer.WriteString("name", Name);
            });
        }
    }

    /// <summary>Declare a node, <c>[nodes.&lt;name&gt;]</c>.</summary>
    public sealed record AddNode(string Name) : ClusterEdit
    {
        /// <inheritdoc />
        public override string ToJson()
        {
            return Json("add-node", writer => writer.WriteString("name", Name));
        }
    }

    // {"<act>":{...}}, through the one writer (JsonText). Its default encoder
    // is the one the reflection serializer used, so the bytes are the same.
    private static string Json(string act, Action<Utf8JsonWriter> body)
    {
        return JsonText.Document(writer =>
        {
            writer.WriteStartObject(act);
            body(writer);
            writer.WriteEndObject();
        });
    }

    private static void Strings(Utf8JsonWriter writer, string name, IReadOnlyList<string> words)
    {
        writer.WriteStartArray(name);

        foreach (string word in words)
        {
            writer.WriteStringValue(word);
        }

        writer.WriteEndArray();
    }

    // "values": [{"key": [...], "value": "..."}], what an entry or an element
    // holds.
    private static void Fields(Utf8JsonWriter writer, IReadOnlyList<ArtifactField> values)
    {
        writer.WriteStartArray("values");

        foreach (ArtifactField given in values)
        {
            writer.WriteStartObject();
            Strings(writer, "key", given.Key);
            writer.WriteString("value", given.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
