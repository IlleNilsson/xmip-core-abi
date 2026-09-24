using Tomlyn.Syntax;

namespace Xmip.Surface.Test;

/// <summary>
/// Editing a document in its syntax tree: a value changes and nothing else
/// does (open problem 25, row b).
/// </summary>
public sealed class TomlDocumentTest
{
    private const string Document = """
        # heading
        [service]
        name = "a" # trailing

        # before the rows
        [[row]]
        key = "one"
        [[row.sub]]
        key = "inner"

        # before the second row
        [[row]]
        key = "two"

        [other]
        "dotted.key" = true
        """;

    [Fact]
    public void ASetReplacesTheValueAndKeepsItsComment()
    {
        DocumentSyntax document = TomlDocument.Parse(Document);
        TableSyntax service = TomlDocument.Tables<TableSyntax>(document, "service").Single();

        TomlDocument.Set(service, "name", "b \"quoted\"");

        Assert.Equal(
            Document.Replace("name = \"a\"", "name = \"b \\\"quoted\\\"\"", StringComparison.Ordinal),
            document.ToString());
        Assert.Equal("b \"quoted\"", TomlDocument.Text(service, "name"));
    }

    [Fact]
    public void AKeyIsMatchedWholeAndByItsSegments()
    {
        DocumentSyntax document = TomlDocument.Parse(Document);
        TableSyntax other = TomlDocument.Tables<TableSyntax>(document, "other").Single();

        Assert.Null(TomlDocument.Flag(other, "dotted"));
        Assert.Equal(["dotted.key"], TomlDocument.KeyPath(other.Items.GetChild(0)!.Key));
        Assert.Equal(2, TomlDocument.Tables<TableArraySyntax>(document, "row").Count());
        Assert.Single(TomlDocument.Tables<TableArraySyntax>(document, "row", "sub"));
    }

    [Fact]
    public void ARemovedRowTakesItsOwnTablesAndLeavesTheRestAsWritten()
    {
        DocumentSyntax document = TomlDocument.Parse(Document);
        TableArraySyntax first = TomlDocument.Tables<TableArraySyntax>(document, "row").First();

        TomlDocument.Remove(document, first);

        // Its tables go; every comment stays, the one before the next row too.
        string expected = Document.Replace(
            "[[row]]\nkey = \"one\"\n[[row.sub]]\nkey = \"inner\"\n",
            "",
            StringComparison.Ordinal);
        Assert.Equal(expected, document.ToString());
    }

    [Fact]
    public void AnInsertedRowJoinsItsGroupBeforeTheNextTablesComments()
    {
        DocumentSyntax document = TomlDocument.Parse(Document);
        TableArraySyntax added = new();
        TomlDocument.Set(added, "key", "three");

        TomlDocument.Insert(document, added, "row");

        Assert.Equal(
            Document.Replace(
                "key = \"two\"\n",
                "key = \"two\"\n\n[[row]]\nkey = \"three\"\n",
                StringComparison.Ordinal),
            document.ToString());
    }

    [Fact]
    public void TextThatIsNotTomlIsAFormatException()
    {
        Assert.Throws<FormatException>(() => TomlDocument.Parse("[service\nname ="));
    }
}
