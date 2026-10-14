namespace Xmip.Abi.Operate;

/// <summary>
/// How this process reaches the runtime's native library, chosen once by
/// <see cref="NativeRuntime"/> for the platform it runs on.
/// </summary>
internal enum NativeLoading
{
    /// <summary>A desktop or server: a copy of the file at the path found is
    /// loaded, never the build output itself.</summary>
    Copy,

    /// <summary>Android: the library is packaged in the application
    /// (<c>lib/&lt;abi&gt;/libxmip_core_runtime.so</c>) and loaded by its
    /// name.</summary>
    ByName,

    /// <summary>iOS: the library is linked statically into the application
    /// from an xcframework, and its exports are the main program's.</summary>
    MainProgram,
}
