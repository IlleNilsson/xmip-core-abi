namespace Xmip.Abi.Operate;

/// <summary>The System Process declarations standing in a directory, and which
/// directory that was (<see cref="RuntimeProcesses.Read"/>).</summary>
public sealed record ProcessDeclarations(
    string Directory, IReadOnlyList<ProcessStanding> Processes);
