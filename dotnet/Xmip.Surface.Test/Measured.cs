namespace Xmip.Surface.Test;

/// <summary>
/// The tests that hold a time to the millisecond rule: run alone, with no
/// other test of this project beside them, so what they measure is what
/// Xmip does and not the load of the run (CONTRIBUTING.md: *apart from
/// load*).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class Measured
{
    /// <summary>The collection's name.</summary>
    public const string Name = "Measured";
}
