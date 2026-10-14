using System.Runtime.InteropServices;

namespace Xmip.Abi.Operate;

/// <summary>
/// The runtime's native library: its file name on this platform, and the one
/// way this binding loads and releases it. <see cref="Operator"/> and
/// <see cref="RuntimeRules"/> both load through here, and no host loads it
/// any other way.
/// </summary>
/// <remarks>
/// The platform is asked here and nowhere else (<see cref="Loading"/>).
///
/// On a desktop or a server a copy is loaded, never the build output itself.
/// A loaded library is locked for as long as this process lives, and the
/// path configured in development is the runtime's own <c>target/debug</c> —
/// so every GUI left running made the next <c>cargo build</c> fail with a
/// locked .dll, and the fix was always "stop the GUI first". Copying costs
/// one file write and removes the hazard for good. The copy is read sharing
/// write and delete, so it never refuses the build writing the library at
/// that moment either: a plain copy shares read only, and a write landing
/// during it failed (a surface's watch reading a library as the next one was
/// written; found 2026-10-12). A library caught half written does not load
/// and says why; the write's end is a change the watch notices, and it is
/// read again.
///
/// On a phone there is no file to copy. Android carries the library inside
/// the application package, and it is loaded by its name; iOS links it
/// statically from an xcframework, and its exports are the main program's
/// (ADR-0014, amendment 2026-10-14). <c>build/Xmip.Abi.Native.props</c> puts
/// it there for a host that imports it. A path found by a surface's rule
/// names no library on a phone and is kept only to be shown.
/// </remarks>
public static class NativeRuntime
{
    /// <summary>The library's file name on this platform.</summary>
    public static string FileName =>
        OperatingSystem.IsWindows()
            ? "xmip_core_runtime.dll"
            : OperatingSystem.IsMacOS()
                ? "libxmip_core_runtime.dylib"
                : "libxmip_core_runtime.so";

    /// <summary>How this process loads the library.</summary>
    internal static NativeLoading Loading { get; } =
        For(android: OperatingSystem.IsAndroid(), ios: OperatingSystem.IsIOS());

    /// <summary>The way for a platform, with the platform in hand.</summary>
    internal static NativeLoading For(bool android, bool ios)
    {
        return ios
            ? NativeLoading.MainProgram
            : android
                ? NativeLoading.ByName
                : NativeLoading.Copy;
    }

    /// <summary>Load the runtime's library — on a desktop a copy of the one at
    /// <paramref name="path"/>, on a phone the one packaged with the
    /// application. False, with the reason in words, when there is none, it
    /// cannot be read now or it does not load.</summary>
    internal static bool TryLoad(string path, out nint library, out string reason)
    {
        return Loading switch
        {
            NativeLoading.MainProgram => TryMainProgram(out library, out reason),
            NativeLoading.ByName => TryByName(out library, out reason),
            _ => TryCopy(path, out library, out reason),
        };
    }

    /// <summary>Release what <see cref="TryLoad"/> loaded. The main program
    /// is never released: it is the application itself.</summary>
    internal static void Free(nint library)
    {
        if (Loading != NativeLoading.MainProgram)
        {
            NativeLibrary.Free(library);
        }
    }

    private static bool TryMainProgram(out nint library, out string reason)
    {
        library = NativeLibrary.GetMainProgramHandle();
        reason = string.Empty;
        return true;
    }

    private static bool TryByName(out nint library, out string reason)
    {
        if (!NativeLibrary.TryLoad(
            FileName, typeof(NativeRuntime).Assembly, searchPath: null, out library))
        {
            reason = $"{FileName} is not packaged with this application";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryCopy(string path, out nint library, out string reason)
    {
        library = 0;

        string copy = Path.Combine(
            Path.GetTempPath(),
            $"xmip-abi-{Guid.NewGuid():n}-{Path.GetFileName(path)}");

        try
        {
            using FileStream source = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using FileStream target = new(copy, FileMode.CreateNew, FileAccess.Write);
            source.CopyTo(target);
        }
        catch (Exception missing)
            when (missing is FileNotFoundException or DirectoryNotFoundException)
        {
            reason = $"no runtime library at {path}";
            return false;
        }
        catch (IOException unread)
        {
            reason = $"{path} could not be read: {unread.Message}";
            return false;
        }

        if (!NativeLibrary.TryLoad(copy, out library))
        {
            reason = $"{path} could not be loaded";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
