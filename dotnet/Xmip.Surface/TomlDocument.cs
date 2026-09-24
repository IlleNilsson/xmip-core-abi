using Microsoft.Extensions.Configuration;
using Tomlyn.Extensions.Configuration;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace Xmip.Surface;

/// <summary>
/// The one way a .NET surface reads a TOML document (ADR-0052 clause 1). On
/// disk the estate is TOML — a host's <c>xmip.gui.toml</c>, a node
/// configuration, a published snapshot — and JSON is reserved for memory and
/// the wire; so there is one reader, and no host adds a JSON source.
/// </summary>
/// <remarks>
/// A surface that edits a document — the desktop's node configuration editor
/// — edits its syntax tree here (<see cref="Parse"/>): a value is replaced in
/// place and everything else, comments, blank lines and the order of keys and
/// tables included, is written back exactly as it was read. Keys are matched
/// whole, segment by segment, and strings are unescaped on read and escaped on
/// write by the TOML library, never by hand. Added 2026-09-24 (open problem
/// 25, row b), when the editor's own reader matched keys by prefix and a
/// round trip through the library's model lost every comment.
/// </remarks>
public static class TomlDocument
{
    /// <summary>Read one document. A file that is not there reads as an empty
    /// document; whoever asked decides whether that is a problem.</summary>
    public static IConfigurationRoot Read(string path)
    {
        return new ConfigurationBuilder()
            .AddTomlFile(path, optional: true, reloadOnChange: false)
            .Build();
    }

    /// <summary>Add a document to a host's configuration, watched for change.
    /// A relative path resolves the way the host's builder resolves files —
    /// its content root.</summary>
    public static IConfigurationBuilder Add(
        IConfigurationBuilder builder, string path, bool optional)
    {
        return builder.AddTomlFile(path, optional, reloadOnChange: true);
    }

    /// <summary>A path a document names: absolute as written, relative joined
    /// to <paramref name="basePath"/>, which is the directory the document's
    /// paths are written from.</summary>
    public static string Resolve(string path, string basePath)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(basePath, path));
    }

    /// <summary>A document's syntax tree, to edit in place; its
    /// <c>ToString()</c> is the document again, trivia and all.</summary>
    /// <exception cref="FormatException">The text is not TOML; the message
    /// says where.</exception>
    public static DocumentSyntax Parse(string text)
    {
        DocumentSyntax document = SyntaxParser.Parse(text, "document.toml", validate: true);

        return document.HasErrors
            ? throw new FormatException(string.Join("; ", document.Diagnostics))
            : document;
    }

    /// <summary>A key as the path it names, each segment unquoted:
    /// <c>a."b.c"</c> is <c>["a", "b.c"]</c>.</summary>
    public static IReadOnlyList<string> KeyPath(KeySyntax? key)
    {
        if (key is null)
        {
            return [];
        }

        List<string> path = [Segment(key.Key)];
        foreach (DottedKeyItemSyntax dotted in key.DotKeys)
        {
            path.Add(Segment(dotted.Key));
        }

        return path;
    }

    /// <summary>The <c>[name]</c> table or <c>[[name]]</c> rows whose key is
    /// exactly <paramref name="path"/>, in document order.</summary>
    public static IEnumerable<T> Tables<T>(DocumentSyntax document, params string[] path)
        where T : TableSyntaxBase
    {
        foreach (TableSyntaxBase table in document.Tables)
        {
            if (table is T typed && KeyPath(table.Name).SequenceEqual(path, StringComparer.Ordinal))
            {
                yield return typed;
            }
        }
    }

    /// <summary>The string <paramref name="key"/> holds in a table, or
    /// <see langword="null"/> when it is absent or not a string.</summary>
    public static string? Text(TableSyntaxBase table, string key)
    {
        return Find(table, key)?.Value is StringValueSyntax text ? text.Value : null;
    }

    /// <summary>The boolean <paramref name="key"/> holds in a table, or
    /// <see langword="null"/> when it is absent or not a boolean.</summary>
    public static bool? Flag(TableSyntaxBase table, string key)
    {
        return Find(table, key)?.Value is BooleanValueSyntax flag ? flag.Value : null;
    }

    /// <summary>Set <paramref name="key"/> to a string, or remove it when
    /// <paramref name="value"/> is <see langword="null"/>. A comment trailing
    /// the old value trails the new one.</summary>
    public static void Set(TableSyntaxBase table, string key, string? value)
    {
        Set(table, key, value is null ? null : new StringValueSyntax(value));
    }

    /// <summary>Set <paramref name="key"/> to a boolean, or remove it when
    /// <paramref name="value"/> is <see langword="null"/>.</summary>
    public static void Set(TableSyntaxBase table, string key, bool? value)
    {
        Set(table, key, value is { } flag ? new BooleanValueSyntax(flag) : null);
    }

    /// <summary>Put a table into the document after the last table of the
    /// group it joins — the tables whose key is <paramref name="path"/> and
    /// those beneath them — or at the end when there is none. The blank lines
    /// and comments that stood before the next table still stand before it.</summary>
    public static void Insert(DocumentSyntax document, TableSyntaxBase table, params string[] path)
    {
        bool rows = table is TableArraySyntax;
        table.OpenBracket ??= SyntaxFactory.Token(
            rows ? TokenKind.OpenBracketDouble : TokenKind.OpenBracket);
        table.Name = Key(path);
        table.CloseBracket ??= SyntaxFactory.Token(
            rows ? TokenKind.CloseBracketDouble : TokenKind.CloseBracket);
        table.EndOfLineToken ??= SyntaxFactory.NewLine();

        List<TableSyntaxBase> tables = [.. document.Tables];
        int after = tables.FindLastIndex(existing => Within(KeyPath(existing.Name), path));
        int at = after < 0 ? tables.Count : after + 1;

        // A blank line between it and what comes before, when anything does.
        if (at > 0 || document.KeyValues.ChildrenCount > 0)
        {
            _ = table.AddLeadingTriviaNewLine();
        }

        SyntaxToken? before = at > 0
            ? LastEndOfLine(tables[at - 1])
            : document.KeyValues.ChildrenCount is > 0 and int count
                ? document.KeyValues.GetChild(count - 1)!.EndOfLineToken ??= SyntaxFactory.NewLine()
                : null;
        Carry(before, LastEndOfLine(table));

        tables.Insert(at, table);
        Replace(document.Tables, tables);
    }

    /// <summary>Take a table out of the document with the tables beneath it
    /// (a row's <c>[[row.sub]]</c> tables), keeping the blank lines and
    /// comments that stood before the next table.</summary>
    public static void Remove(DocumentSyntax document, TableSyntaxBase table)
    {
        List<TableSyntaxBase> tables = [.. document.Tables];
        int first = tables.IndexOf(table);
        if (first < 0)
        {
            return;
        }

        IReadOnlyList<string> path = KeyPath(table.Name);
        int last = first;
        while (last + 1 < tables.Count
            && KeyPath(tables[last + 1].Name) is { } next
            && next.Count > path.Count
            && next.Take(path.Count).SequenceEqual(path, StringComparer.Ordinal))
        {
            last++;
        }

        if (first > 0)
        {
            Carry(LastEndOfLine(tables[last]), LastEndOfLine(tables[first - 1]));
        }

        tables.RemoveRange(first, last - first + 1);
        Replace(document.Tables, tables);
    }

    private static void Set(TableSyntaxBase table, string key, ValueSyntax? value)
    {
        KeyValueSyntax? existing = Find(table, key);

        if (value is null)
        {
            if (existing is not null)
            {
                int index = IndexOf(table.Items, existing);
                SyntaxToken? previous = index == 0
                    ? table.EndOfLineToken
                    : table.Items.GetChild(index - 1)!.EndOfLineToken;
                Carry(existing.EndOfLineToken, previous);
                table.Items.RemoveChild(existing);
            }

            return;
        }

        if (existing is not null)
        {
            if (existing.Value is { } old && Token(old) is { } was && Token(value) is { } now)
            {
                now.TrailingTrivia = was.TrailingTrivia;
            }

            existing.Value = value;
            return;
        }

        KeyValueSyntax added = new(key, value);
        Carry(LastEndOfLine(table), added.EndOfLineToken);
        table.Items.Add(added);
    }

    private static KeyValueSyntax? Find(TableSyntaxBase table, string key)
    {
        foreach (KeyValueSyntax item in table.Items)
        {
            if (KeyPath(item.Key) is [{ } only] && string.Equals(only, key, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    private static int IndexOf(SyntaxList<KeyValueSyntax> items, KeyValueSyntax item)
    {
        for (int index = 0; index < items.ChildrenCount; index++)
        {
            if (ReferenceEquals(items.GetChild(index), item))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool Within(IReadOnlyList<string> candidate, string[] path)
    {
        return candidate.Count >= path.Length
            && candidate.Take(path.Length).SequenceEqual(path, StringComparer.Ordinal);
    }

    private static string Segment(BareKeyOrStringValueSyntax? key)
    {
        return key switch
        {
            BareKeySyntax bare => bare.Key?.Text ?? "",
            StringValueSyntax quoted => quoted.Value ?? "",
            _ => "",
        };
    }

    private static KeySyntax Key(string[] path)
    {
        KeySyntax key = new(path[0]);
        foreach (string segment in path.Skip(1))
        {
            key.DotKeys.Add(new DottedKeyItemSyntax(segment));
        }

        return key;
    }

    private static SyntaxToken? Token(ValueSyntax value)
    {
        return value switch
        {
            StringValueSyntax text => text.Token,
            BooleanValueSyntax flag => flag.Token,
            _ => null,
        };
    }

    // The line end of a table's last line: its last key's, else its header's.
    // A document that does not end in a line break gets one, so what follows
    // starts on a line of its own.
    private static SyntaxToken LastEndOfLine(TableSyntaxBase table)
    {
        int count = table.Items.ChildrenCount;
        if (count > 0)
        {
            KeyValueSyntax last = table.Items.GetChild(count - 1)!;
            return last.EndOfLineToken ??= SyntaxFactory.NewLine();
        }

        return table.EndOfLineToken ??= SyntaxFactory.NewLine();
    }

    // What trails a line end — the blank lines and comments before whatever
    // comes next — moves to the line end that now comes last.
    private static void Carry(SyntaxToken? from, SyntaxToken? to)
    {
        if (from?.TrailingTrivia is not { Count: > 0 } trivia || to is null)
        {
            return;
        }

        to.TrailingTrivia = [.. to.TrailingTrivia ?? [], .. trivia];
        from.TrailingTrivia = null;
    }

    private static void Replace(SyntaxList<TableSyntaxBase> list, List<TableSyntaxBase> tables)
    {
        while (list.ChildrenCount > 0)
        {
            list.RemoveChildAt(list.ChildrenCount - 1);
        }

        foreach (TableSyntaxBase table in tables)
        {
            list.Add(table);
        }
    }
}
