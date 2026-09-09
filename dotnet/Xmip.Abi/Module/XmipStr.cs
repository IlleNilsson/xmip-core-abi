using System.Runtime.InteropServices;
using System.Text;

namespace Xmip.Abi.Module;

/// <summary>
/// A borrowed UTF-8 string. Section 2 of <c>xmip_module.h</c>: never owned by
/// the receiver, never null-terminated, valid only for the duration of the
/// call it was passed to unless the function documents otherwise.
/// </summary>
/// <remarks>
/// Shared by both boundaries. ADR-0027 clause 1: a string means the same thing
/// to a module author and to an operator surface, or it means nothing to
/// either. To hand a .NET string across a call, see <see cref="Pin"/>.
/// </remarks>
/// <param name="data">The first byte; <c>ptr</c> in the header.</param>
/// <param name="len">The length in bytes.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly unsafe struct XmipStr(byte* data, nuint len)
{
    /// <summary>The first byte; <c>ptr</c> in the header. Null only when
    /// <see cref="Len"/> is 0.</summary>
    public readonly byte* Data = data;

    /// <summary>The length in bytes.</summary>
    public readonly nuint Len = len;

    /// <summary>
    /// Copies out of the borrow. The pointer stops being valid when the call
    /// that produced it returns, so nothing may hold onto it.
    /// </summary>
    public string Read()
    {
        return Data is null || Len == 0
            ? string.Empty
            : Encoding.UTF8.GetString(Data, checked((int)Len));
    }

    /// <summary>
    /// Pins a .NET string as UTF-8 for the duration of one call. Dispose it
    /// when the call returns; native code may not keep the pointer.
    /// </summary>
    public static PinnedStr Pin(string text)
    {
        return new PinnedStr(text);
    }
}
