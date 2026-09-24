using System.Runtime.InteropServices;

namespace Xmip.Abi.Operate;

/// <summary>
/// Loading a runtime library the one way this binding does: a copy, never the
/// build output itself. <see cref="Operator"/> and <see cref="RuntimeRules"/>
/// both load through here.
/// </summary>
/// <remarks>
/// A loaded library is locked for as long as this process lives, and the path
/// configured in development is the runtime's own <c>target/debug</c> — so
/// every GUI left running made the next <c>cargo build</c> fail with a locked
/// .dll, and the fix was always "stop the GUI first". Copying costs one file
/// write and removes the hazard for good.
/// </remarks>
internal static class LibraryCopy
{
    /// <summary>Load a copy of the library at <paramref name="path"/>. False,
    /// with the reason in words, when there is none or it does not load.</summary>
    public static bool TryLoad(string path, out nint library, out string reason)
    {
        library = 0;

        if (!File.Exists(path))
        {
            reason = $"no runtime library at {path}";
            return false;
        }

        string copy = Path.Combine(
            Path.GetTempPath(),
            $"xmip-abi-{Guid.NewGuid():n}-{Path.GetFileName(path)}");
        File.Copy(path, copy);

        if (!NativeLibrary.TryLoad(copy, out library))
        {
            reason = $"{path} could not be loaded";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
