namespace Xmip.Abi.Operate;

/// <summary>
/// One System Process's declaration as it stands (ADR-0053 clause 3): what it
/// said of itself — name, location, purpose and anything else — which process
/// said it, when, from where on disk, and the file it stands in. Read by
/// <c>xmip-core-node</c> and crossed by <see cref="RuntimeProcesses.Read"/>.
/// </summary>
/// <param name="File">The file the declaration stands in.</param>
/// <param name="Name">What the process is: <c>xmip-&lt;what&gt;</c>.</param>
/// <param name="Location">Where in Xmip it belongs.</param>
/// <param name="Purpose"><c>test</c> or <c>runtime</c>.</param>
/// <param name="Pid">The process that said it.</param>
/// <param name="StartedUnix">When, in seconds since the Unix epoch; 0 where it
/// did not say.</param>
/// <param name="Path">Its executable on disk; empty where it did not say.</param>
/// <param name="Said">What else it said of itself, key to text: a Playground
/// node the flags it was started with.</param>
public sealed record ProcessStanding(
    string File,
    string Name,
    string Location,
    string Purpose,
    int Pid,
    long StartedUnix,
    string Path,
    IReadOnlyDictionary<string, string> Said);
