namespace Xmip.Surface;

/// <summary>One step on the path from the cluster down to a scope: what it is
/// called, and the scope to climb back to.</summary>
public sealed record Crumb(string Label, string Scope);
