using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 20 of <c>include/xmip_operate.h</c>, crossed by P/Invoke: the
/// scripts that upgrade each database on a server from one schema version to
/// a later one, adding only (open problem 41; ADR-0015, amendment
/// 2026-10-20). The schema, its versions and the scripts are
/// <c>xmip-core-persist</c>'s; this binds the one call for
/// <c>xmip-cli database</c> and <c>Get-XmipDatabaseScript</c>, which write
/// none of their own.
/// </summary>
/// <remarks>Pure in the header's sense: one instance serves a whole process
/// from any thread. A node's build only, as sections 10 to 16.</remarks>
public sealed unsafe class RuntimeDatabases
{
    private readonly delegate* unmanaged[Cdecl]<XmipStr, uint, uint, byte*, nuint, nuint*, int>
        _upgrade;

    internal RuntimeDatabases(nint library)
    {
        _upgrade = (delegate* unmanaged[Cdecl]<XmipStr, uint, uint, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.DatabaseUpgradeEntrypoint);
    }

    /// <summary>Section 20's symbol, which a node's runtime exports.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
        [OperateAbi.DatabaseUpgradeEntrypoint];

    /// <summary>
    /// The scripts that bring each database on <paramref name="server"/> —
    /// <c>postgresql</c>, <c>mssql</c> or <c>mariadb</c> — from schema version
    /// <paramref name="from"/> to <paramref name="to"/>, zero for this build's;
    /// or, refused, the runtime's sentence and no script.
    /// </summary>
    public DatabaseUpgrade Upgrade(string server, uint from, uint to)
    {
        ArgumentNullException.ThrowIfNull(server);

        byte[] word = Encoding.UTF8.GetBytes(server);
        byte[] room = new byte[RuntimeRemotes.Room];

        while (true)
        {
            nuint length = 0;
            int status;

            fixed (byte* named = word)
            fixed (byte* written = room)
            {
                status = _upgrade(
                    new XmipStr(named, (nuint)word.Length), from, to, written,
                    (nuint)room.Length, &length);
            }

            if (length > (nuint)room.Length)
            {
                room = new byte[(int)length];
                continue;
            }

            string text = Encoding.UTF8.GetString(room, 0, (int)length);

            return status switch
            {
                0 => Answered(server, text),
                (int)XmipStatus.Invalid => new DatabaseUpgrade(server, 0, [], text),
                _ => new DatabaseUpgrade(
                    server, 0, [], $"the runtime answered with {(XmipStatus)status}"),
            };
        }
    }

    private static DatabaseUpgrade Answered(string server, string text)
    {
        using JsonDocument answer = JsonDocument.Parse(text);
        JsonElement root = answer.RootElement;

        return new DatabaseUpgrade(
            server,
            root.GetProperty("version").GetUInt32(),
            [.. root.GetProperty("scripts").EnumerateArray().Select(script => new DatabaseScript(
                script.GetProperty("name").GetString() ?? string.Empty,
                script.GetProperty("text").GetString() ?? string.Empty))],
            null);
    }
}
