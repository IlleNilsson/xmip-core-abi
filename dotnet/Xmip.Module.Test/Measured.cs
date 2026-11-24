namespace Xmip.Module.Test;

/// <summary>A Promote unit for the tests: the content's length under the
/// step's <c>property</c>, its text as <c>Text</c> and whether it is empty
/// as <c>Empty</c>; content reading <c>refuse</c> is refused.</summary>
internal sealed class Measured : PromoteUnit
{
    /// <summary>The one instance the test assembly exports.</summary>
    public static readonly Measured Exported = new();

    public override string Name => "Measured";

    public override IReadOnlyList<UnitSetting> Settings =>
    [
        new("property", SettingKind.Text, "The property the length is promoted into.")
        {
            Default = "Length",
        },
        new("format", SettingKind.Choice, "How the text \"is\" read.")
        {
            Choices = ["text", "bytes"],
            Optional = true,
        },
        new("limit", SettingKind.Integer, "The most bytes read.")
        {
            Default = 4096L,
            Minimum = 1,
        },
    ];

    public override Promotion Open(StepSettings settings)
    {
        string property = settings.Text("property");

        return property.Length == 0
            ? throw new RefusedException("the step's property is empty")
            : new Measuring(property);
    }

    private sealed class Measuring(string property) : Promotion
    {
        public override void Promote(Stream content, Promoted promoted)
        {
            using MemoryStream read = new();
            content.CopyTo(read, 2);
            string text = System.Text.Encoding.UTF8.GetString(read.ToArray());

            if (text == "refuse")
            {
                throw new RefusedException("the content says refuse");
            }

            promoted.Number(property, read.Length);
            promoted.Text("Text", text);
            promoted.Boolean("Empty", read.Length == 0);
        }
    }
}
