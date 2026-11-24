using System.Runtime.InteropServices;

namespace Xmip.Abi.Module;

/// <summary>
/// A pull source. <c>XmipReader</c>, section 5 of <c>xmip_module.h</c>:
/// <c>read</c> returns the bytes written into the buffer, 0 at the end of
/// the stream, or a negative status; a short read is not the end.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct XmipReader
{
    /// <summary><c>ctx</c>, handed back to <see cref="Read"/>.</summary>
    public unsafe void* Ctx;

    /// <summary><c>read</c>.</summary>
    public unsafe delegate* unmanaged[Cdecl]<void*, byte*, nuint, long> Read;
}
