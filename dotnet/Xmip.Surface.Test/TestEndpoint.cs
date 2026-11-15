namespace Xmip.Surface.Test;

/// <summary>
/// A local endpoint's name no other test or run uses: the one place a .NET
/// test takes one from. One file, compiled into every .NET test project that
/// names an endpoint (each links it), so there is one rule.
/// </summary>
/// <remarks>
/// On Linux and macOS the runtime places the endpoint called a name at
/// <c>&lt;folder&gt;&lt;name&gt;/&lt;name&gt;.sock</c>, the name twice, and
/// refuses a socket's path longer than a Unix domain socket holds: 107 bytes
/// on Linux, 103 on macOS (ADR-0014, amendment 2026-11-15). A test's own name
/// and a GUID's 32 digits did not fit under <c>$XDG_RUNTIME_DIR/xmip</c>.
/// Unique to a run by this process's identifier and a count, as the runtime's
/// own tests name theirs (<c>tests/plane/endpoint.rs</c>); a socket a run
/// that ended left behind is replaced by the runtime as it binds.
/// </remarks>
internal static class TestEndpoint
{
    private static int next;

    /// <summary>The name of an endpoint for <paramref name="what"/>, unique to
    /// this run: <c>xmip-&lt;what&gt;-&lt;process&gt;-&lt;n&gt;</c>.</summary>
    public static string Named(string what)
    {
        return $"xmip-{what}-{Environment.ProcessId}-{Interlocked.Increment(ref next)}";
    }
}
