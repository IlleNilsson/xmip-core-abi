using System.Text.Json;

namespace Xmip.Abi.Operate;

/// <summary>
/// A member of the cluster a node does not hear now (ADR-0065, amendment
/// 2026-10-02): <c>observe::Unheard</c>, read from the JSON section 11
/// writes. Its Events do not reach the Event subscriptions on
/// <see cref="By"/> until it is heard again; nothing missing is silent.
/// </summary>
/// <param name="By">The node that does not hear it.</param>
/// <param name="Node">The member not heard.</param>
/// <param name="Since">Since when.</param>
/// <param name="Why">Why, in words.</param>
/// <param name="Said">The one line every surface shows, as the runtime words
/// it: <c>not hearing &lt;node&gt; since &lt;time&gt;: &lt;why&gt;</c>.</param>
public sealed record UnheardRecord(
    string By, string Node, DateTimeOffset Since, string Why, string Said)
{
    /// <summary>The members a JSON array section 11 wrote names.</summary>
    public static IReadOnlyList<UnheardRecord> Read(JsonElement list)
    {
        return
        [
            .. list.EnumerateArray().Select(entry => new UnheardRecord(
                entry.GetProperty("by").GetString() ?? string.Empty,
                entry.GetProperty("node").GetString() ?? string.Empty,
                Operator.FromNanos(entry.GetProperty("since_unix_nanos").GetInt64()),
                entry.GetProperty("why").GetString() ?? string.Empty,
                entry.GetProperty("said").GetString() ?? string.Empty)),
        ];
    }
}
