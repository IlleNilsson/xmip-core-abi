using System.Runtime.InteropServices;
using System.Text;

namespace Xmip.Abi.Module;

/// <summary>
/// A .NET string held still as UTF-8 so an <see cref="XmipStr"/> can borrow
/// it across one call. The header's borrow, seen from the caller's side:
/// valid until disposed, and nothing native may hold the pointer past that.
/// </summary>
/// <remarks>
/// A ref struct, so it cannot escape the frame that made it — which is the
/// lifetime rule the header states, enforced by the compiler rather than by
/// a comment. Use it with <c>using</c>.
/// </remarks>
public ref struct PinnedStr
{
    private GCHandle _handle;
    private readonly nuint _len;

    /// <summary>Pins <paramref name="text"/>, encoded as UTF-8.</summary>
    public PinnedStr(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);

        _handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        _len = (nuint)bytes.Length;
    }

    /// <summary>The borrow to pass. Empty text is an empty borrow, null pointer
    /// and zero length, which is what section 2 permits. Unsafe: the caller
    /// promises not to read it once this is disposed.</summary>
    public readonly unsafe XmipStr Value
    {
        get
        {
            unsafe
            {
                return _len == 0
                    ? default
                    : new XmipStr((byte*)_handle.AddrOfPinnedObject(), _len);
            }
        }
    }

    /// <summary>Releases the pin. Idempotent.</summary>
    public void Dispose()
    {
        // Freed once: IsAllocated is false after, so a second Dispose is none.
        if (_handle.IsAllocated)
        {
            unsafe
            {
                _handle.Free();
            }
        }
    }
}
