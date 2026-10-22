using System.Text.Json;
using System.Text.Json.Nodes;
using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// The cluster's own authority kept in a folder, and a node's certificate
/// carried across an air gap, for every .NET surface (ADR-0034, amendment
/// 2026-10-22): <c>xmip certificate</c>, <c>New-</c>, <c>Get-</c>,
/// <c>Set-</c> and <c>Export-XmipCertificate</c>, and the desktop host's
/// Authority page. Every act, its checks and its files are the runtime's
/// (<c>xmip_operate.h</c> section 23), and the runtime's are the
/// capability's; this asks and reads the answer into a record.
/// </summary>
public static class AuthorityCertificate
{
    /// <summary>The authority kept in <paramref name="authority"/>, made
    /// there for <paramref name="cluster"/> where none is, and the
    /// plain-HTTP address <paramref name="publish"/> it publishes its list
    /// and certificate at, where one is given.</summary>
    public static AuthorityState SetUp(
        RuntimeCertificates certificates, string authority, string cluster, string? publish)
    {
        ArgumentNullException.ThrowIfNull(cluster);

        JsonObject arguments = new()
        {
            ["authority"] = authority,
            ["name"] = NodeCertificate.AuthorityName(cluster),
        };

        if (!string.IsNullOrWhiteSpace(publish))
        {
            arguments["publish"] = publish;
        }

        return State(Asked(certificates, "set-up", arguments), authority);
    }

    /// <summary>The authority kept in <paramref name="authority"/> and every
    /// certificate it signed.</summary>
    public static AuthorityState Inspect(RuntimeCertificates certificates, string authority) =>
        State(Asked(certificates, "inspect", new() { ["authority"] = authority }), authority);

    /// <summary>The leaf of the chain in <paramref name="certificate"/> and
    /// its standing.</summary>
    public static CertificateStanding Read(RuntimeCertificates certificates, string certificate)
    {
        (JsonElement? read, string refused) =
            Asked(certificates, "read", new() { ["certificate"] = certificate });

        return read is { } it
            ? new CertificateStanding(
                Text(it, "certificate"), Text(it, "serial"), Text(it, "subject"),
                Names(it), Moment(it, "notBefore"), Moment(it, "notAfter"),
                Moment(it, "renewAt"), Text(it, "standing"), string.Empty)
            : new CertificateStanding(
                certificate, string.Empty, string.Empty, [], default, default, default,
                string.Empty, refused);
    }

    /// <summary>The certificate <paramref name="serial"/> the authority in
    /// <paramref name="authority"/> signed, revoked for
    /// <paramref name="reason"/> — an RFC 5280 word, <c>keyCompromise</c> —
    /// and listed at once.</summary>
    public static AuthorityRecord Revoke(
        RuntimeCertificates certificates, string authority, string serial, string reason)
    {
        (JsonElement? revoked, string refused) = Asked(certificates, "revoke", new()
        {
            ["authority"] = authority,
            ["serial"] = serial,
            ["reason"] = reason,
        });

        return revoked is { } it
            ? Record(it)
            : new AuthorityRecord(
                serial, string.Empty, [], string.Empty, default, default, default,
                string.Empty, null, string.Empty, refused);
    }

    /// <summary>The authority's certificate, <c>pem</c> or <c>der</c>, or
    /// its revocation list, <c>crl</c>, written to <paramref name="path"/>,
    /// never over a file.</summary>
    public static AuthorityExport Export(
        RuntimeCertificates certificates, string authority, string path, string format)
    {
        (JsonElement? written, string refused) = Asked(certificates, "export", new()
        {
            ["authority"] = authority,
            ["path"] = path,
            ["format"] = format,
        });

        return written is { } it
            ? new AuthorityExport(Text(it, "written"), Text(it, "format"), [], string.Empty)
            : new AuthorityExport(path, format, [], refused);
    }

    /// <summary>The whole authority copied into the empty folder
    /// <paramref name="folder"/>, each file private: the copy is the same
    /// authority.</summary>
    public static AuthorityExport BackUp(
        RuntimeCertificates certificates, string authority, string folder)
    {
        (JsonElement? copied, string refused) = Asked(certificates, "back-up", new()
        {
            ["authority"] = authority,
            ["folder"] = folder,
        });

        return copied is { } it
            ? new AuthorityExport(folder, "backup", Strings(it, "copied"), string.Empty)
            : new AuthorityExport(folder, "backup", [], refused);
    }

    /// <summary>A fresh key and a request for the node
    /// <paramref name="node"/>, reached by its own name and every one of
    /// <paramref name="names"/>, written into <paramref name="folder"/> on
    /// the node, to carry to the authority.</summary>
    public static CarriedRequest Request(
        RuntimeCertificates certificates, string node, IReadOnlyList<string> names,
        string folder)
    {
        (JsonElement? asked, string refused) = Asked(certificates, "request", new()
        {
            ["node"] = node,
            ["names"] = Array(names),
            ["folder"] = folder,
        });

        return asked is { } it
            ? new CarriedRequest(
                node, Text(it, "request"), Text(it, "key"), Names(it), string.Empty)
            : new CarriedRequest(node, string.Empty, string.Empty, [], refused);
    }

    /// <summary>The carried request in the file <paramref name="request"/>
    /// signed by the authority in <paramref name="authority"/> for ninety
    /// days, where it asks for exactly <paramref name="names"/> when any are
    /// given; the chain written to <paramref name="written"/> where a file
    /// is named, never over one.</summary>
    public static CarriedSigned Sign(
        RuntimeCertificates certificates, string authority, string request,
        IReadOnlyList<string> names, string? written)
    {
        JsonObject arguments = new()
        {
            ["authority"] = authority,
            ["request"] = request,
            ["names"] = Array(names),
        };

        if (written is not null)
        {
            arguments["out"] = written;
        }

        (JsonElement? signed, string refused) = Asked(certificates, "sign", arguments);

        return signed is { } it
            ? new CarriedSigned(
                request, Text(it, "serial"), Names(it), Moment(it, "notAfter"),
                written, Text(it, "chain"), string.Empty)
            : new CarriedSigned(request, string.Empty, [], default, null, string.Empty, refused);
    }

    /// <summary>The chain in the file <paramref name="certificate"/>
    /// imported into the node's <paramref name="folder"/>, the one its
    /// request was made in, once every check holds.</summary>
    public static CarriedImport Import(
        RuntimeCertificates certificates, string certificate, string folder)
    {
        (JsonElement? imported, string refused) = Asked(certificates, "import", new()
        {
            ["certificate"] = certificate,
            ["folder"] = folder,
        });

        return imported is { } it
            ? new CarriedImport(
                Text(it, "certificate"), Text(it, "key"), Text(it, "anchors"),
                Text(it, "serial"), Moment(it, "notAfter"), Moment(it, "renewAt"),
                Text(it, "anchorFingerprint"),
                it.GetProperty("anchorTaken").GetBoolean(), string.Empty)
            : new CarriedImport(
                string.Empty, string.Empty, string.Empty, string.Empty, default, default,
                string.Empty, false, refused);
    }

    // One act asked: the answer, or the refusal said in words.
    private static (JsonElement? Answer, string Refused) Asked(
        RuntimeCertificates certificates, string act, JsonObject arguments)
    {
        ArgumentNullException.ThrowIfNull(certificates);

        if (!certificates.Authority(
                act, arguments.ToJsonString(), out string answer, out string refused))
        {
            return (null, $"REFUSED: {refused}");
        }

        using JsonDocument read = JsonDocument.Parse(answer);
        return (read.RootElement.Clone(), string.Empty);
    }

    private static AuthorityState State(
        (JsonElement? Answer, string Refused) asked, string authority)
    {
        if (asked.Answer is not { } it)
        {
            return new AuthorityState(
                authority, string.Empty, default, default, string.Empty, string.Empty, 0,
                default, 0, [], asked.Refused);
        }

        JsonElement list = it.GetProperty("list");
        AuthorityRecord[] issued = it.GetProperty("issued").EnumerateArray()
            .Select(Record).ToArray();

        return new AuthorityState(
            Text(it, "authority"), Text(it, "subject"), Moment(it, "notBefore"),
            Moment(it, "notAfter"), Text(it, "fingerprint"), Text(it, "published"),
            list.GetProperty("number").GetInt64(), Moment(list, "nextUpdate"),
            list.GetProperty("revoked").GetInt32(), issued, string.Empty);
    }

    private static AuthorityRecord Record(JsonElement it)
    {
        JsonElement revoked = it.GetProperty("revoked");
        bool isRevoked = revoked.ValueKind == JsonValueKind.Object;
        string standing = it.TryGetProperty("standing", out JsonElement said)
            ? said.GetString() ?? string.Empty
            : isRevoked ? "revoked" : string.Empty;

        return new AuthorityRecord(
            Text(it, "serial"), Text(it, "subject"), Names(it), Text(it, "purpose"),
            Moment(it, "notBefore"), Moment(it, "notAfter"), Moment(it, "renewAt"), standing,
            isRevoked ? Moment(revoked, "at") : null,
            isRevoked ? Text(revoked, "reason") : string.Empty,
            string.Empty);
    }

    private static string Text(JsonElement it, string name) =>
        it.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static DateTimeOffset Moment(JsonElement it, string name) =>
        DateTimeOffset.FromUnixTimeSeconds(it.GetProperty(name).GetInt64());

    private static string[] Names(JsonElement it) => Strings(it, "names");

    private static string[] Strings(JsonElement it, string name) =>
        it.TryGetProperty(name, out JsonElement list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(one => one.GetString() ?? string.Empty).ToArray()
            : [];

    private static JsonArray Array(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        JsonArray array = [];

        foreach (string name in names)
        {
            array.Add(JsonValue.Create(name));
        }

        return array;
    }
}
