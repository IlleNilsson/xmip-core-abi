using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Abi;

/// <summary>
/// The two boundaries this build speaks: the module boundary a Module plugs
/// into and the operator boundary a surface drives from, versioned apart
/// (ADR-0027 clause 2). Answered from the binding's constants; nothing is
/// loaded. <c>xmip-cli abi</c> renders it and <c>Get-XmipAbi</c> emits it, so
/// both say both boundaries — until 2026-09-24 the cmdlet said only the first.
/// </summary>
/// <param name="ModuleVersion">The module handshake version
/// (<see cref="ModuleAbi.AbiVersion"/>).</param>
/// <param name="ModuleEntrypoint">The one symbol every module exports.</param>
/// <param name="ModuleLibraryFileName">How this platform names a loadable
/// module, shown for <see cref="ExampleModule"/>.</param>
/// <param name="OperateVersion">The operator boundary version
/// (<see cref="OperateAbi.Version"/>).</param>
/// <param name="OperateEntrypoint">The one symbol a runtime exports for the
/// surfaces that watch.</param>
public sealed record AbiBoundaries(
    uint ModuleVersion,
    string ModuleEntrypoint,
    string ModuleLibraryFileName,
    uint OperateVersion,
    string OperateEntrypoint)
{
    /// <summary>The module named as the example of platform naming.</summary>
    public const string ExampleModule = "xmip_core_transport_file";

    /// <summary>The boundaries of this build.</summary>
    public static AbiBoundaries Current { get; } = new(
        ModuleAbi.AbiVersion,
        ModuleAbi.Entrypoint,
        ModuleAbi.LibraryFileName(ExampleModule),
        OperateAbi.Version,
        OperateAbi.Entrypoint);
}
