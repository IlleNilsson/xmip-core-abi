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
///
/// The copy is read sharing write and delete, so it never refuses the build
/// writing the library at that moment either: a plain copy shares read only,
/// and a write landing during it failed (a surface's watch reading a library
/// as the next one was written; found 2026-10-12). A library caught half
/// written does not load and says why; the write's end is a change the watch
/// notices, and it is read again.
/// </remarks>
internal static class LibraryCopy
{
    /// <summary>Load a copy of the library at <paramref name="path"/>. False,
    /// with the reason in words, when there is none, it cannot be read now
    /// or it does not load.</summary>
    public static bool TryLoad(string path, out nint library, out string reason)
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
