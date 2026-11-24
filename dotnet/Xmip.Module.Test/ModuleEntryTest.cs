namespace Xmip.Module.Test;

/// <summary>The <c>[[modules]]</c> entry written from a unit's declaration,
/// in the shape the runtime reads (<c>abi::ModuleManifest</c>).</summary>
public sealed class ModuleEntryTest
{
    [Fact]
    public void TheEntryDeclaresTheLibraryTheUnitAndItsSettings()
    {
        string entry = ModuleEntry.Of(Measured.Exported, "Orders", "1.2.3", "Orders");

        Assert.StartsWith(
            "[[modules]]\nname = \"Orders\"\nstart = true\n",
            entry,
            StringComparison.Ordinal);
        Assert.Contains(
            "[modules.manifest.identity]\nname = \"Orders\"\nversion = \"1.2.3\"\n",
            entry,
            StringComparison.Ordinal);
        Assert.Contains(
            "capability = \"promote\"\nexecution_host = \"c-abi\"",
            entry,
            StringComparison.Ordinal);
        Assert.Contains(
            "library_path = \"Orders\"\nsymbol = \"xmip_create_module_v1\"\n",
            entry,
            StringComparison.Ordinal);
        Assert.Contains(
            "[[modules.manifest.units]]\nname = \"Measured\"\ncapability = \"promote\"\n" +
            "stages = [\"receive\"]\noffline = true\n",
            entry,
            StringComparison.Ordinal);
        Assert.Contains(
            "name = \"property\"\nkind = \"text\"\npresence = \"default\"\ndefault = \"Length\"\n" +
            "meaning = \"The property the length is promoted into.\"\n",
            entry,
            StringComparison.Ordinal);
        Assert.Contains(
            "name = \"format\"\nkind = \"choice\"\nchoices = [\"text\", \"bytes\"]\n" +
            "presence = \"optional\"\nmeaning = \"How the text \\\"is\\\" read.\"\n",
            entry,
            StringComparison.Ordinal);
        Assert.Contains(
            "name = \"limit\"\nkind = \"integer\"\nminimum = 1\npresence = \"default\"\n" +
            "default = 4096\n",
            entry,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AStepsSettingsAreReadAsTheyWereGivenAndOneNotGivenIsRefused()
    {
        StepSettings settings = StepSettings.Read(
            "receive",
            """{"member":"number","lines":3,"strict":true,"namespaces":{"o":"urn:o"}}""");

        Assert.Equal("receive", settings.Stage);
        Assert.Equal("number", settings.Text("member"));
        Assert.Equal(3, settings.Integer("lines"));
        Assert.True(settings.Boolean("strict"));
        Assert.Equal([new("o", "urn:o")], settings.Table("namespaces"));
        Assert.False(settings.Has("absent"));
        Assert.Equal(
            "the setting 'lines' is not text",
            Assert.Throws<RefusedException>(() => settings.Text("lines")).Message);
        Assert.Equal(
            "the step gives no setting 'absent'",
            Assert.Throws<RefusedException>(() => settings.Integer("absent")).Message);
        Assert.Throws<RefusedException>(() => StepSettings.Read("receive", "[1]"));
        Assert.Throws<RefusedException>(() => StepSettings.Read("receive", "{"));
    }
}
