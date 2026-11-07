using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Xmip.Surface;

/// <summary>
/// One JSON document as text, written by hand through
/// <see cref="Utf8JsonWriter"/>: the one writer every surface's JSON goes
/// through. No serializer: the command line publishes ahead-of-time, and a
/// reflection-based serializer is the first thing the trimmer takes away.
/// </summary>
public static class JsonText
{
    /// <summary>A document for a program: one line, no indentation, so it is
    /// one JSON Lines record when several follow each other (ADR-0014
    /// clause 10).</summary>
    public static string Document(Action<Utf8JsonWriter> body)
    {
        return Document(body, default);
    }

    /// <summary>A document written with <paramref name="options"/>: an MCP
    /// message (ADR-0071) escapes only what JSON requires, since the command's
    /// document it carries is itself a string inside it.</summary>
    public static string Document(Action<Utf8JsonWriter> body, JsonWriterOptions options)
    {
        ArgumentNullException.ThrowIfNull(body);

        // The bytes are decoded where they were written, never copied out
        // into an array of their own first.
        ArrayBufferWriter<byte> buffer = new();

        using (Utf8JsonWriter writer = new(buffer, options))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
