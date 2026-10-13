using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// What a section 11 call answered: a status the runtime refused with, in its
/// words, and the JSON a listing call wrote into a buffer.
/// </summary>
internal static unsafe class EventAnswer
{
    internal delegate int Writer(byte* into, nuint cap, nuint* length);

    // What a call that writes JSON into a buffer wrote, asked again with room
    // where the first buffer was too small.
    internal static byte[] Written(Writer write)
    {
        byte[] buffer = new byte[4096];

        while (true)
        {
            nuint length = 0;
            int status;

            fixed (byte* into = buffer)
            {
                status = write(into, (nuint)buffer.Length, &length);
            }

            Refused(status, "a list");

            if (length <= (nuint)buffer.Length)
            {
                return buffer[..(int)length];
            }

            buffer = new byte[checked((int)length)];
        }
    }

    // What the runtime refused, in its status's words; nothing on XMIP_OK.
    internal static void Refused(int status, string what)
    {
        if (status == 0)
        {
            return;
        }

        XmipStatus said = (XmipStatus)status;

        throw said is XmipStatus.Invalid or XmipStatus.Malformed
            ? new ArgumentException($"The runtime refused {what}: {said.Explain()}")
            : new InvalidOperationException($"The runtime answered {what} with {said}: " +
                said.Explain());
    }
}
