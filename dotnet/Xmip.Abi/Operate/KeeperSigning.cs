using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// What section 17's kept open calls back into .NET on the runtime's own
/// threads (ADR-0034, amendment 2026-10-15): the keeper's signature over
/// what a handshake signs — ECDSA on P-256 with SHA-256, DER — written into
/// the runtime's room. A keeper that throws refuses the handshake with its
/// message. Nothing may unwind into Rust.
/// </summary>
internal static unsafe class KeeperSigning
{
    // The runtime calls this from any thread while the handle lives; the
    // context is a GCHandle the RemoteNode frees after the handle.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    internal static int Sign(
        void* context, XmipStr message, byte* written, nuint cap, nuint* length)
    {
        byte[] answer;
        int status;

        try
        {
            if (GCHandle.FromIntPtr((nint)context).Target is not Func<byte[], byte[]> sign)
            {
                throw new InvalidOperationException("no keeper is held for this handle");
            }

            byte[] signed = new byte[(int)message.Len];

            if (message.Len > 0)
            {
                new ReadOnlySpan<byte>(message.Data, (int)message.Len).CopyTo(signed);
            }

            answer = sign(signed);
            status = (int)XmipStatus.Ok;
        }
#pragma warning disable CA1031 // Whatever it is, it must not unwind into Rust.
        catch (Exception refused)
#pragma warning restore CA1031
        {
            answer = Encoding.UTF8.GetBytes(refused.Message);
            status = (int)XmipStatus.Auth;
        }

        *length = (nuint)answer.Length;
        answer.AsSpan(0, (int)Math.Min((nuint)answer.Length, cap)).CopyTo(
            new Span<byte>(written, (int)cap));
        return status;
    }
}
