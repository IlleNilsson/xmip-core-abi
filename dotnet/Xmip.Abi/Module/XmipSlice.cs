using System.Runtime.InteropServices;

namespace Xmip.Abi.Module;

/// <summary>
/// A borrowed byte range. Section 2 of <c>xmip_module.h</c>: never owned by
/// the receiver, never null-terminated, valid only for the duration of the
/// call it was passed to. <see cref="XmipStr"/> is the same layout, holding
/// UTF-8.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct XmipSlice
{
    /// <summary>The first byte; <c>ptr</c> in the header. Null only when
    /// <see cref="Len"/> is 0. Unsafe: nothing says the bytes are still
    /// there.</summary>
    public readonly unsafe byte* Data;

    /// <summary>The length in bytes.</summary>
    public readonly nuint Len;

    /// <summary>A borrow of <paramref name="len"/> bytes at
    /// <paramref name="data"/>. Unsafe: the caller promises they are there
    /// for as long as the borrow is read.</summary>
    /// <param name="data">The first byte; <c>ptr</c> in the header.</param>
    /// <param name="len">The length in bytes.</param>
    public unsafe XmipSlice(byte* data, nuint len)
    {
        unsafe
        {
            Data = data;
        }

        Len = len;
    }
}
