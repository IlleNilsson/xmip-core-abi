namespace Xmip.Abi.Operate;

/// <summary>
/// What <see cref="RuntimeDatabases.Upgrade"/> answered for a server: this
/// build's schema version and one script to each database, or the runtime's
/// refusal and none (section 20).
/// </summary>
/// <param name="Server">The server it was asked for, as the line named it.</param>
/// <param name="Version">This build's schema version; zero where refused.</param>
/// <param name="Scripts">One to each database, in the order to run them.</param>
/// <param name="Refused">The runtime's sentence; null where answered.</param>
public sealed record DatabaseUpgrade(
    string Server, uint Version, IReadOnlyList<DatabaseScript> Scripts, string? Refused)
{
    /// <summary>Whether the scripts were written.</summary>
    public bool Ok => Refused is null;
}
