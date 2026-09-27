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
    /// <summary>
    /// What a module said when asked, or why it could not be asked: the one
    /// object <c>xmip-cli probe</c> renders and <c>Get-XmipModuleDescriptor</c>
    /// emits.
    /// </summary>
    /// <param name="Library">The library probed, as given.</param>
    /// <param name="Status">What the entrypoint returned; <c>NotFound</c>
    /// where the library could not be loaded at all.</param>
    /// <param name="Provider">The descriptor's provider.</param>
    /// <param name="Module">The descriptor's module name.</param>
    /// <param name="Standard">The descriptor's standard, empty for core.</param>
    /// <param name="AbiVersion">The abi_version the module says it speaks.</param>
    /// <param name="TraitVersion">The trait's major.minor.</param>
    /// <param name="ModuleVersion">The module's major.minor.patch.</param>
    /// <param name="LastError">The module's last error, or the status's
    /// meaning where it refused.</param>
    /// <param name="Unloadable">Why the library could not be loaded — it is
    /// not there, not a library for this platform, or exports no
    /// entrypoint; empty when it loaded.</param>
    public sealed record Result(
        string Library,
        XmipStatus Status,
        string Provider,
        string Module,
        string Standard,
        uint AbiVersion,
        string TraitVersion,
        string ModuleVersion,
        string LastError,
        string Unloadable = "")
    {
        /// <summary>Whether the library loaded and its entrypoint was
        /// asked.</summary>
        public bool Loaded => Unloadable.Length == 0;

        /// <summary>The status as one line an operator reads.</summary>
        public string Meaning => Status.Explain();

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
            { Loaded: false } or { Status: not XmipStatus.Ok } => string.Empty,
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
    /// <paramref name="log"/>, or nowhere when it is null. A library that
    /// cannot be loaded — not there, not for this platform, no
    /// <see cref="ModuleAbi.Entrypoint"/> — is an answer, not an exception:
    /// <see cref="Result.Unloadable"/> says which, judged here once for every
    /// surface.
    /// </summary>
    public static Result Probe(string libraryPath, Action<string>? log)
    {
        try
        {
            return Loaded(libraryPath, log);
        }
        catch (Exception failure) when (failure
            is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return Failed(libraryPath, XmipStatus.NotFound) with { Unloadable = failure.Message };
        }
    }

    private static Result Loaded(string libraryPath, Action<string>? log)
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
                return Failed(libraryPath, status);
            }

            try
            {
                return Describe(libraryPath, module);
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

    private static Result Describe(string libraryPath, XmipModule module)
    {
        XmipModuleDescriptor descriptor = module.Descriptor;

        string lastError = module.LastError is null
            ? string.Empty
            : module.LastError(module.State).Read();

        return new Result(
            libraryPath,
            XmipStatus.Ok,
            descriptor.Provider.Read(),
            descriptor.Module.Read(),
            descriptor.Standard.Read(),
            descriptor.AbiVersion,
            $"{descriptor.TraitMajor}.{descriptor.TraitMinor}",
            $"{descriptor.ModuleMajor}.{descriptor.ModuleMinor}.{descriptor.ModulePatch}",
            lastError);
    }

    private static Result Failed(string libraryPath, XmipStatus status)
    {
        return new Result(
            libraryPath,
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
