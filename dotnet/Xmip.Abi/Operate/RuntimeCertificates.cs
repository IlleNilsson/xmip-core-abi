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
/// The rules are <c>xmip-core-provision</c>'s; nothing here keeps one.
/// </summary>
/// <remarks>Pure in the header's sense: one instance serves a whole process
/// from any thread.</remarks>
public sealed unsafe class RuntimeCertificates
{
    private readonly nint _library;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int> _request;
    private readonly delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int>
        _sign;

    internal RuntimeCertificates(nint library)
    {
        _library = library;
        _request = (delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.OperatorRequestEntrypoint);
        bool signs = NativeLibrary.TryGetExport(
            library, OperateAbi.OperatorSignEntrypoint, out nint sign);
        _sign = signs
            ? (delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, nuint, nuint*, int>)sign
            : null;
    }

    /// <summary>The symbols every build exports.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
        [OperateAbi.OperatorRequestEntrypoint];

    /// <summary>The symbols only a node's build exports.</summary>
    public static IReadOnlyList<string> NodeEntrypoints { get; } =
        [OperateAbi.OperatorSignEntrypoint];

    /// <summary>
    /// A fresh key and an operator's request for <paramref name="person"/>:
    /// the key's <c>PRIVATE KEY</c> PEM and the <c>CERTIFICATE REQUEST</c>
    /// PEM; or, where the runtime refuses the name, false with its sentence
    /// in <paramref name="refused"/>.
    /// </summary>
    public bool Request(
        string person, out string key, out string request, out string refused)
    {
        ArgumentNullException.ThrowIfNull(person);

        (int status, string text) = Answer(
            [person], (pack, data, written, cap, length) =>
                _request(pack.Borrow(data, 0), written, cap, length));
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

    private static string Refusal(int status, string text)
    {
        return status == (int)XmipStatus.Invalid
            ? text
            : $"the runtime answered with {(XmipStatus)status}";
    }
}
