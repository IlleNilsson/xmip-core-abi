using System.Text.Json;

namespace Xmip.Surface.Test;

/// <summary>
/// <see cref="ClusterEdit"/> writes the header's JSON by hand, so the
/// executable can publish ahead-of-time. Each edit's text is the one the
/// reflection-based serializer wrote for it before, byte for byte, escaping
/// included: here, where nothing is published ahead-of-time, the serializer
/// is the measure.
/// </summary>
public sealed class ClusterEditTest
{
    // Quotes, a backslash, markup and a word beyond ASCII: what the encoder
    // escapes is where two writers would differ first.
    private const string Awkward = "\"<a & 'b'>\\ + café ✓";

    private static readonly string[] Section = ["locations", Awkward];
    private static readonly string[] Key = ["address", "port"];

    private static readonly ArtifactField[] Values =
    [
        new(["path"], "'/in'", "string"),
        new(["mode", Awkward], Awkward, "string"),
    ];

    [Fact]
    public void SetIsTheSerializersText()
    {
        Same(
            new { set = new { section = Section, key = Key, value = Awkward } },
            new ClusterEdit.Set(Section, Key, Awkward));
    }

    [Fact]
    public void RemoveIsTheSerializersText()
    {
        Same(
            new { remove = new { section = Section, key = Key } },
            new ClusterEdit.Remove(Section, Key));
    }

    [Fact]
    public void AddEntryIsTheSerializersTextWithValuesAndWithout()
    {
        Same(
            new Dictionary<string, object>
            {
                ["add-entry"] = new { section = Section, name = Awkward, values = Fields(Values) },
            },
            new ClusterEdit.AddEntry(Section, Awkward, Values));
        Same(
            new Dictionary<string, object>
            {
                ["add-entry"] = new { section = Section, name = "n", values = Fields([]) },
            },
            new ClusterEdit.AddEntry(Section, "n"));
    }

    [Fact]
    public void AddElementIsTheSerializersText()
    {
        Same(
            new Dictionary<string, object>
            {
                ["add-element"] = new { section = Section, key = Key, values = Fields(Values) },
            },
            new ClusterEdit.AddElement(Section, Key, Values));
    }

    [Fact]
    public void RemoveEntryIsTheSerializersTextEitherWay()
    {
        foreach (bool references in new[] { false, true })
        {
            Same(
                new Dictionary<string, object>
                {
                    ["remove-entry"] = new { section = Section, references },
                },
                new ClusterEdit.RemoveEntry(Section, references));
        }
    }

    [Fact]
    public void RenameAndAddNodeAreTheSerializersText()
    {
        Same(
            new { rename = new { section = Section, name = Awkward } },
            new ClusterEdit.Rename(Section, Awkward));
        Same(
            new Dictionary<string, object> { ["add-node"] = new { name = Awkward } },
            new ClusterEdit.AddNode(Awkward));
    }

    private static object[] Fields(IEnumerable<ArtifactField> values)
    {
        return [.. values.Select(given => new { key = given.Key, value = given.Value })];
    }

    private static void Same(object expected, ClusterEdit edit)
    {
        Assert.Equal(JsonSerializer.Serialize(expected), edit.ToJson());
    }
}
