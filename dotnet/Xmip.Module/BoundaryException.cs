namespace Xmip.Module;

/// <summary>
/// What the node answered across the boundary when it was not OK — its
/// reader or its sink — carried out through the unit's own code and
/// answered as that status, never as the unit's defect.
/// </summary>
internal sealed class BoundaryException(int status, string message) : Exception(message)
{
    /// <summary>The node's status.</summary>
    public int Status { get; } = status;
}
