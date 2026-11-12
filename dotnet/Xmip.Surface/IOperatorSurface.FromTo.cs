namespace Xmip.Surface;

// From and to on every observation (the owner, 2026-11-10): read once here,
// for every view, command and cmdlet; the one rule is the runtime's
// (observe::topology::from_to, section 8; ADR-0052, amendment 2026-11-11).
public partial interface IOperatorSurface
{
    /// <summary>
    /// Where the work observed at <paramref name="scope"/> came from and where
    /// it went, as the runtime says it for the nearest thing at or above the
    /// scope that the publication draws — a record beneath a Receive Location
    /// has its Location's. <see cref="Abi.Operate.FromTo.None"/> where nothing
    /// at or above it is drawn, or the surface publishes no topology.
    /// </summary>
    public Abi.Operate.FromTo FromTo(string scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (Topology().Paths is not { Count: > 0 } paths)
        {
            return Abi.Operate.FromTo.None;
        }

        for (string at = ScopeTree.Join(ScopeTree.Parts(scope)); ; at = ScopeTree.Parent(at))
        {
            if (paths.TryGetValue(at, out Abi.Operate.FromTo? said))
            {
                return said;
            }

            if (at == ScopeTree.Root)
            {
                return Abi.Operate.FromTo.None;
            }
        }
    }
}
