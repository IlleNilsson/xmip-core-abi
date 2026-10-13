namespace Xmip.Surface;

/// <summary>
/// The surface over a node on this machine, reached at its local endpoint —
/// a named pipe on Windows, a Unix domain socket elsewhere — over gRPC
/// (ADR-0014, amendment 2026-10-13): a <see cref="ListenerOperator"/> with no
/// certificate, because the operating system admits only the node's account
/// and the machine's administrators to the endpoint and proves to the node
/// which user this process runs as. The node takes every act as that user.
/// Every .NET surface operates a node on its own machine this way; until
/// 2026-10-13 it loaded the runtime into its own process instead, which
/// operated only the nodes that process ran.
/// </summary>
/// <param name="endpoint">The node's local endpoint: a name, or the pipe's or
/// socket's path. With none, a surface that answers nothing and says why
/// (<see cref="Unnamed"/>).</param>
public sealed class LocalOperator(string? endpoint)
    : ListenerOperator(
        "LOCAL", endpoint ?? string.Empty, Opened(endpoint, out string reason), reason)
{
    /// <summary>What every surface says when nothing names a node to operate
    /// on this machine.</summary>
    public const string Unnamed =
        "no node is named: give -Local or --local the node's local endpoint, " +
        "as its [operate] local names it, or name a surface in the document";

    /// <summary>What every surface says when <c>--local</c> or
    /// <c>-Local</c> names no endpoint; null when it names one.</summary>
    public static string? Refusal(string? endpoint)
    {
        return string.IsNullOrWhiteSpace(endpoint)
            ? "A local node needs its local endpoint, like xmip-node1."
            : null;
    }

    private static Abi.Operate.RemoteNode? Opened(string? endpoint, out string reason)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            reason = Unnamed;
            return null;
        }

        return RuntimeLibrary.Rules.Remotes.OpenLocal(endpoint, out reason);
    }
}
