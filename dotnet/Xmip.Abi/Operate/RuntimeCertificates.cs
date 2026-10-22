using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 19 of <c>include/xmip_operate.h</c>, crossed by P/Invoke: a
/// person's certificate (ADR-0034, amendment 2026-10-15). A key and a
/// request naming the person made, which every build carries, the phone's
/// too; and a request signed by the authority a node's
/// <c>[provision.operator]</c> names, which only a node's build carries —
/// asked of a remote build, it is refused in words (<see cref="RemoteBuild"/>).
/// Where the key never leaves its platform's keeper, the request is made in
/// two halves around the keeper's signature, <see cref="Information"/> and
/// <see cref="Assemble"/>, which every build carries too, so no surface
/// writes DER. Section 22 beside it: a node's key and certificate issued by
/// the cluster's own authority, <see cref="Issue"/>, a node's build only.
/// Section 23 beside them: the authority kept in a folder and a node's
/// certificate carried across an air gap, <see cref="Authority"/>, a node's
/// build only.
/// The rules are <c>xmip-core-provision</c>'s; nothing here keeps one.
/// </summary>
/// <remarks>Pure in the header's sense: one instance serves a whole process
/// from any thread.</remarks>
public sealed unsafe class RuntimeCertificates
{
    private readonly nint _library;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int>
        _request;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int>
        _sign;

    // An xmip_slice of bytes has the layout of an xmip_str (the header's
    // typedef), so the two halves cross as XmipStr over bytes.
    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int>
        _information;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int>
        _assemble;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte, byte*, nuint, nuint*, int>
        _keep;
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, XmipStr, XmipStr, XmipStr, XmipStr, byte*, nuint, nuint*, int> _issue;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int>
        _authority;

    internal RuntimeCertificates(nint library)
    {
        _library = library;
        _request = (delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.OperatorRequestEntrypoint);
        _information = (delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.OperatorInformationEntrypoint);
        _assemble = (delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.OperatorAssembleEntrypoint);
        _keep = (delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.OperatorKeepEntrypoint);
        bool signs = NativeLibrary.TryGetExport(
            library, OperateAbi.OperatorSignEntrypoint, out nint sign);
        _sign = signs
            ? (delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int>)sign
            : null;
        bool issues = NativeLibrary.TryGetExport(
            library, OperateAbi.NodeIssueEntrypoint, out nint issue);
        _issue = issues
            ? (delegate* unmanaged[Cdecl]<
                XmipStr, XmipStr, XmipStr, XmipStr, XmipStr, byte*, nuint, nuint*, int>)issue
            : null;
        bool kept = NativeLibrary.TryGetExport(
            library, OperateAbi.AuthorityEntrypoint, out nint authority);
        _authority = kept
            ? (delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int>)authority
            : null;
    }

    /// <summary>The symbols every build exports.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
    [
        OperateAbi.OperatorRequestEntrypoint,
        OperateAbi.OperatorInformationEntrypoint,
        OperateAbi.OperatorAssembleEntrypoint,
        OperateAbi.OperatorKeepEntrypoint,
    ];

    /// <summary>The symbols only a node's build exports.</summary>
    public static IReadOnlyList<string> NodeEntrypoints { get; } =
        [
            OperateAbi.OperatorSignEntrypoint, OperateAbi.NodeIssueEntrypoint,
            OperateAbi.AuthorityEntrypoint,
        ];

    /// <summary>
    /// A fresh key and an operator's request for <paramref name="person"/>,
    /// written by the runtime into <paramref name="folder"/> as private
    /// files — the key never enters this process's memory — and the paths of
    /// the two files: <paramref name="key"/>, <c>PRIVATE KEY</c> PEM, and
    /// <paramref name="request"/>, <c>CERTIFICATE REQUEST</c> PEM. Or, where
    /// the runtime refuses the name or a file is there already, false with
    /// its sentence in <paramref name="refused"/> and nothing written.
    /// </summary>
    public bool Request(
        string person, string folder, out string key, out string request, out string refused)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(folder);

        (int status, string text) = Answer(
            [person, folder], (pack, data, written, cap, length) =>
                _request(pack.Borrow(data, 0), pack.Borrow(data, 1), written, cap, length));
        key = string.Empty;
        request = string.Empty;
        refused = string.Empty;

        if (status != 0)
        {
            refused = Refusal(status, text);
            return false;
        }

        using JsonDocument answer = JsonDocument.Parse(text);
        key = answer.RootElement.GetProperty("key").GetString() ?? string.Empty;
        request = answer.RootElement.GetProperty("request").GetString() ?? string.Empty;
        return true;
    }

    /// <summary>
    /// What an operator's request for <paramref name="person"/> signs over a
    /// key its platform's keeper holds, DER: <paramref name="publicKey"/> as
    /// the keeper hands it — <c>SubjectPublicKeyInfo</c> DER, or the bare
    /// uncompressed point. The keeper signs these bytes and
    /// <see cref="Assemble"/> makes the request; or false with the runtime's
    /// sentence in <paramref name="refused"/>.
    /// </summary>
    public bool Information(
        string person, byte[] publicKey, out byte[] information, out string refused)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(publicKey);

        Utf8Pack pack = new([person]);
        (int status, byte[] bytes) = Bytes(
            publicKey, [], (key, _, written, cap, length) =>
            {
                fixed (byte* data = pack.Bytes)
                {
                    return _information(pack.Borrow(data, 0), key, written, cap, length);
                }
            });
        information = status == 0 ? bytes : [];
        refused = status == 0 ? string.Empty : Refusal(status, Encoding.UTF8.GetString(bytes));
        return status == 0;
    }

    /// <summary>
    /// The request, <c>CERTIFICATE REQUEST</c> PEM, that
    /// <paramref name="information"/> and the keeper's
    /// <paramref name="signature"/> over it — ECDSA with SHA-256, DER — make,
    /// the signature verified first; or false with the runtime's sentence.
    /// </summary>
    public bool Assemble(
        byte[] information, byte[] signature, out string request, out string refused)
    {
        ArgumentNullException.ThrowIfNull(information);
        ArgumentNullException.ThrowIfNull(signature);

        (int status, byte[] bytes) = Bytes(
            information, signature, (first, second, written, cap, length) =>
                _assemble(first, second, written, cap, length));
        string text = Encoding.UTF8.GetString(bytes);
        request = status == 0 ? text : string.Empty;
        refused = status == 0 ? string.Empty : Refusal(status, text);
        return status == 0;
    }

    /// <summary>
    /// <paramref name="text"/> — a chain the authority signed, or the anchors
    /// a surface trusts, PEM — kept whole at the file <paramref name="path"/>
    /// by the runtime: written beside it and synced, then, where
    /// <paramref name="replace"/>, renamed over what was there; else never
    /// over a file already there. Or false with the runtime's sentence in
    /// <paramref name="refused"/>.
    /// </summary>
    public bool Keep(string path, string text, bool replace, out string refused)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(text);

        byte over = replace ? (byte)1 : (byte)0;
        (int status, string said) = Answer(
            [path, text], (pack, data, written, cap, length) =>
                _keep(pack.Borrow(data, 0), pack.Borrow(data, 1), over, written, cap, length));
        refused = status == 0 ? string.Empty : Refusal(status, said);
        return status == 0;
    }

    /// <summary>
    /// The chain, PEM, leaf first, for the request <paramref name="request"/>
    /// signed by the authority the node configuration at
    /// <paramref name="configuration"/> names; or false with the runtime's
    /// sentence in <paramref name="refused"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The library is a remote
    /// build, which signs nothing.</exception>
    public bool Sign(string configuration, string request, out string chain, out string refused)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(request);

        if (_sign is null)
        {
            throw RemoteBuild.Refused(_library, NodeEntrypoints);
        }

        (int status, string text) = Answer(
            [configuration, request], (pack, data, written, cap, length) =>
                _sign(pack.Borrow(data, 0), pack.Borrow(data, 1), written, cap, length));
        chain = status == 0 ? text : string.Empty;
        refused = status == 0 ? string.Empty : Refusal(status, text);
        return status == 0;
    }

    /// <summary>
    /// A fresh key and certificate for the node <paramref name="node"/>,
    /// issued by the authority kept in <paramref name="authority"/> — made
    /// there, called <paramref name="name"/>, where none is — for every one
    /// of <paramref name="names"/> and the node's own, written by the
    /// runtime into <paramref name="folder"/> as <c>node.pem</c>,
    /// <c>node.key</c> and <c>anchors.pem</c>, private files, and their
    /// paths; or false with the runtime's sentence in
    /// <paramref name="refused"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The library is a remote
    /// build, which issues nothing.</exception>
    public bool Issue(
        string authority, string name, string node, IReadOnlyList<string> names, string folder,
        out NodeIssued issued, out string refused)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(folder);

        if (_issue is null)
        {
            throw RemoteBuild.Refused(_library, NodeEntrypoints);
        }

        (int status, string text) = Answer(
            [authority, name, node, string.Join(',', names), folder],
            (pack, data, written, cap, length) => _issue(
                pack.Borrow(data, 0), pack.Borrow(data, 1), pack.Borrow(data, 2),
                pack.Borrow(data, 3), pack.Borrow(data, 4), written, cap, length));
        issued = new NodeIssued(string.Empty, string.Empty, string.Empty);
        refused = string.Empty;

        if (status != 0)
        {
            refused = Refusal(status, text);
            return false;
        }

        using JsonDocument answer = JsonDocument.Parse(text);
        string Written(string file) =>
            answer.RootElement.GetProperty(file).GetString() ?? string.Empty;
        issued = new NodeIssued(Written("certificate"), Written("key"), Written("anchors"));
        return true;
    }

    /// <summary>
    /// The act <paramref name="act"/> on the cluster's authority, or on a
    /// node's certificate carried across an air gap, its
    /// <paramref name="arguments"/> a JSON object: the runtime's answer, JSON,
    /// in <paramref name="answer"/>; or false with its sentence in
    /// <paramref name="refused"/> (section 23).
    /// </summary>
    /// <exception cref="InvalidOperationException">The library is a remote
    /// build, which keeps no authority.</exception>
    public bool Authority(
        string act, string arguments, out string answer, out string refused)
    {
        ArgumentNullException.ThrowIfNull(act);
        ArgumentNullException.ThrowIfNull(arguments);

        if (_authority is null)
        {
            throw RemoteBuild.Refused(_library, NodeEntrypoints);
        }

        (int status, string text) = Answer(
            [act, arguments], (pack, data, written, cap, length) =>
                _authority(pack.Borrow(data, 0), pack.Borrow(data, 1), written, cap, length));
        answer = status == 0 ? text : string.Empty;
        refused = status == 0 ? string.Empty : Refusal(status, text);
        return status == 0;
    }

    private delegate int Call(Utf8Pack pack, byte* data, byte* written, nuint cap, nuint* length);

    // Asks once with room for any answer this section gives, and again with
    // the true length where it did not fit.
    private static (int Status, string Text) Answer(string[] texts, Call call)
    {
        Utf8Pack pack = new(texts);
        byte[] room = new byte[RuntimeRemotes.Room];

        while (true)
        {
            nuint length = 0;
            int status;

            fixed (byte* data = pack.Bytes)
            fixed (byte* written = room)
            {
                status = call(pack, data, written, (nuint)room.Length, &length);
            }

            if (length <= (nuint)room.Length)
            {
                string text = Encoding.UTF8.GetString(room, 0, (int)length);
                Array.Clear(room);
                return (status, text);
            }

            Array.Clear(room);
            room = new byte[(int)length];
        }
    }

    private delegate int Lent(
        XmipStr first, XmipStr second, byte* written, nuint cap, nuint* length);

    // Two byte ranges lent for one call, asked again with the true length
    // where the answer did not fit; the answer as the runtime wrote it.
    private static (int Status, byte[] Bytes) Bytes(byte[] first, byte[] second, Lent call)
    {
        byte[] room = new byte[RuntimeRemotes.Room];

        while (true)
        {
            nuint length = 0;
            int status;

            fixed (byte* one = first)
            fixed (byte* two = second)
            fixed (byte* written = room)
            {
                status = call(
                    new XmipStr(one, (nuint)first.Length), new XmipStr(two, (nuint)second.Length),
                    written, (nuint)room.Length, &length);
            }

            if (length <= (nuint)room.Length)
            {
                return (status, room[..(int)length]);
            }

            room = new byte[(int)length];
        }
    }

    private static string Refusal(int status, string text)
    {
        return status == (int)XmipStatus.Invalid
            ? text
            : $"the runtime answered with {(XmipStatus)status}";
    }
}
