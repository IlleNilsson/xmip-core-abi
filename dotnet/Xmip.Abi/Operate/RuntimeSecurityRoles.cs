using System.Runtime.InteropServices;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 18 of <c>include/xmip_operate.h</c>, crossed by P/Invoke
/// (ADR-0009, amendments 2026-10-03 and 2026-10-06): the role a word names,
/// what a role is called and may do, and the one check every act passes —
/// the gate a node's operate listener asks too, so a screen and a node
/// refuse in the same words. A role crosses as its number, least first:
/// 0 Observer, 1 Operator, 2 Developer.
/// </summary>
/// <remarks>Pure in the header's sense: one instance serves a whole process
/// from any thread.</remarks>
public sealed unsafe class RuntimeSecurityRoles
{
    private readonly delegate* unmanaged[Cdecl]<XmipStr, int*, int> _named;
    private readonly delegate* unmanaged[Cdecl]<int, XmipStr*, XmipStr*, int> _words;
    private readonly delegate* unmanaged[Cdecl]<
        XmipStr, byte, int, XmipStr, XmipStr, byte*, nuint, nuint*, int> _admitted;

    internal RuntimeSecurityRoles(nint library)
    {
        _named = (delegate* unmanaged[Cdecl]<XmipStr, int*, int>)
            NativeLibrary.GetExport(library, OperateAbi.SecurityRoleNamedEntrypoint);
        _words = (delegate* unmanaged[Cdecl]<int, XmipStr*, XmipStr*, int>)
            NativeLibrary.GetExport(library, OperateAbi.SecurityRoleWordsEntrypoint);
        _admitted = (delegate* unmanaged[Cdecl]<
            XmipStr, byte, int, XmipStr, XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.ActAdmittedEntrypoint);
    }

    /// <summary>The symbols this binds, each of which a runtime must export.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
    [
        OperateAbi.SecurityRoleNamedEntrypoint,
        OperateAbi.SecurityRoleWordsEntrypoint,
        OperateAbi.ActAdmittedEntrypoint,
    ];

    /// <summary>The role <paramref name="word"/> names, Observer's (0) for
    /// any other word.</summary>
    public int Named(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        Utf8Pack pack = new([word]);
        int role = 0;

        fixed (byte* data = pack.Bytes)
        {
            _ = _named(pack.Borrow(data, 0), &role);
        }

        return role;
    }

    /// <summary>What <paramref name="role"/> is called and what it may do;
    /// empty for a number that is no role.</summary>
    public (string Word, string Description) Words(int role)
    {
        XmipStr word = default;
        XmipStr description = default;

        return _words(role, &word, &description) == 0
            ? (word.Read(), description.Read())
            : (string.Empty, string.Empty);
    }

    /// <summary>
    /// Whether <paramref name="who"/> — the identity the connection proved,
    /// null where nothing did — holding <paramref name="role"/> may
    /// <paramref name="act"/> on <paramref name="target"/>; where not, the
    /// refusal in <paramref name="refused"/>.
    /// </summary>
    public bool Admitted(string? who, int role, string act, string target, out string refused)
    {
        ArgumentNullException.ThrowIfNull(act);
        ArgumentNullException.ThrowIfNull(target);

        Utf8Pack pack = new([who ?? string.Empty, act, target]);
        byte[] text = new byte[RuntimeRemotes.Room];
        nuint length = 0;
        int status;

        fixed (byte* data = pack.Bytes)
        fixed (byte* written = text)
        {
            status = _admitted(
                pack.Borrow(data, 0), who is null ? (byte)0 : (byte)1, role, pack.Borrow(data, 1),
                pack.Borrow(data, 2), written, (nuint)text.Length, &length);
        }

        refused = RuntimeRemotes.Said(text, length);
        return status switch
        {
            0 => true,
            (int)XmipStatus.Auth => false,
            _ => throw new InvalidOperationException(
                $"the runtime answered an act's admission with {(XmipStatus)status}"),
        };
    }
}
