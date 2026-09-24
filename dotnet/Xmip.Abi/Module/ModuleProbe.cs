using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Xmip.Abi.Module;

/// <summary>
/// Loads a module through the C ABI and reports what it says about itself.
/// </summary>
/// <remarks>
/// ADR-0012 leaves one thing open: "a conformance suite able to drive the seven
/// rules in its section 11 from outside a module". This is the first of those
/// rules — that the module exports the entrypoint, accepts the host's
/// abi_version, fills the descriptor, and destroys cleanly.
///
/// It links no Xmip code. A module written in C, Go or C# is probed exactly the
/// same way, which is the property the boundary exists to have.
///
/// One probe serves every surface, and deliberately so: two surfaces answering
/// one question through one boundary is the property ADR-0014 exists to
/// guarantee. The one thing they differ on is where a module's log lines go —
/// the cli writes them to stderr as they arrive, a cmdlet has a verbose stream
/// — so the caller passes a sink and the probe judges nothing about it.
/// </remarks>
public static unsafe class ModuleProbe
{
    /// <summary>What a module said when asked.</summary>
    public sealed record Result(
        XmipStatus Status,
        string Provider,
        string Module,
        string Standard,
        uint AbiVersion,
        string TraitVersion,
        string ModuleVersion,
        string LastError)
    {
        /// <summary>
        /// What a module that loaded got wrong, in one sentence; empty when it
        /// conforms, or when it did not load and <see cref="Status"/> says why.
        /// Judged here once for every surface: until 2026-09-24 the cli judged
        /// it and the cmdlet did not, so one module conformed in PowerShell
        /// and failed on the command line.
        /// </summary>
        /// <remarks>Section 4 of the header: standard is empty only when
        /// provider is <c>core</c>.</remarks>
        public string Complaint => this switch
        {
            { Status: not XmipStatus.Ok } => string.Empty,
            { AbiVersion: not ModuleAbi.AbiVersion } =>
                $"Loaded, and disagrees: the module says {AbiVersion}, " +
                $"this build speaks {ModuleAbi.AbiVersion}.",
            { Provider: "core", Standard.Length: > 0 } =>
                $"A core module named a standard ('{Standard}'). " +
                "ADR-0011 leaves that slot empty for core.",
            _ => string.Empty,
        };

        /// <summary>The module loaded, said what it is, and got nothing
        /// wrong.</summary>
        public bool Conforms => Status == XmipStatus.Ok && Complaint.Length == 0;
    }

    /// <summary>
    /// Where log lines go during the one probe in flight.
    /// </summary>
    /// <remarks>
    /// ThreadStatic rather than passed through, because the callback crosses
    /// native code and an UnmanagedCallersOnly method captures nothing. One
    /// probe per thread at a time is the contract, and every surface honours
    /// it by construction.
    /// </remarks>
    [ThreadStatic]
    private static Action<string>? _log;

    /// <summary>
    /// Load <paramref name="libraryPath"/>, create the module, read its
    /// descriptor and destroy it. Log lines the module emits on the way go to
    /// <paramref name="log"/>, or nowhere when it is null.
    /// </summary>
    /// <exception cref="DllNotFoundException">The library could not be loaded.</exception>
    /// <exception cref="BadImageFormatException">It is not a library for this
    /// platform.</exception>
    /// <exception cref="EntryPointNotFoundException">It exports no
    /// <see cref="ModuleAbi.Entrypoint"/>.</exception>
    public static Result Probe(string libraryPath, Action<string>? log)
    {
        nint handle = NativeLibrary.Load(libraryPath);
        _log = log;

        try
        {
            if (!NativeLibrary.TryGetExport(handle, ModuleAbi.Entrypoint, out nint symbol))
            {
                throw new EntryPointNotFoundException(
                    $"{libraryPath} exports no {ModuleAbi.Entrypoint}. " +
                    "Every conforming module exports exactly that symbol.");
            }

            delegate* unmanaged[Cdecl]<XmipHost*, XmipModule*, XmipStatus> create =
                (delegate* unmanaged[Cdecl]<XmipHost*, XmipModule*, XmipStatus>)symbol;

            XmipHost host = new()
            {
                AbiVersion = ModuleAbi.AbiVersion,
                Ctx = null,
                Log = &OnLog,
                Cancelled = &OnCancelled,
                JourneyId = &OnJourneyId,
            };

            XmipModule module = default;
            XmipStatus status = create(&host, &module);

            if (status != XmipStatus.Ok)
            {
                // The module returned a status and left *out untouched, so
                // there is nothing to read and nothing to destroy.
                return Failed(status);
            }

            try
            {
                return Describe(module);
            }
            finally
            {
                // Section 7. The module frees its own state; the host never
                // does, because no allocator is shared across this boundary.
                if (module.Destroy is not null)
                {
                    module.Destroy(module.State);
                }
            }
        }
        finally
        {
            _log = null;
            NativeLibrary.Free(handle);
        }
    }

    private static Result Describe(XmipModule module)
    {
        XmipModuleDescriptor descriptor = module.Descriptor;

        string lastError = module.LastError is null
            ? string.Empty
            : module.LastError(module.State).Read();

        return new Result(
            XmipStatus.Ok,
            descriptor.Provider.Read(),
            descriptor.Module.Read(),
            descriptor.Standard.Read(),
            descriptor.AbiVersion,
            $"{descriptor.TraitMajor}.{descriptor.TraitMinor}",
            $"{descriptor.ModuleMajor}.{descriptor.ModuleMinor}.{descriptor.ModulePatch}",
            lastError);
    }

    private static Result Failed(XmipStatus status)
    {
        return new Result(
            status,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            status.Explain());
    }

    // ----------------------------------------------------------------------
    // Host callbacks. These are called from native code, so nothing may throw
    // across them — an exception unwinding into C is undefined behaviour, the
    // same rule the header applies to a Rust panic.
    // ----------------------------------------------------------------------

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnLog(void* ctx, int level, XmipStr target, XmipStr message)
    {
        try
        {
            XmipLogLevel name = (XmipLogLevel)level;
            _log?.Invoke($"[{name}] {target.Read()}: {message.Read()}");
        }
        catch
        {
            // Swallowed on purpose. A failure to record a log line is not
            // worth taking the process down, and there is no way to report it
            // from here.
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OnCancelled(void* ctx)
    {
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static XmipStr OnJourneyId(void* ctx)
    {
        // Empty: a probe runs outside any Journey, which is exactly the case
        // the header says to answer empty for.
        return default;
    }
}
