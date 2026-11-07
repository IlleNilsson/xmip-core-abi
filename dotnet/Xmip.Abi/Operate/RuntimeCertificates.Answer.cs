using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

// How each of section 19's calls is asked: with room for its answer, and
// again at the true length where it did not fit.
public sealed partial class RuntimeCertificates
{
    private delegate int Call(Utf8Pack pack, byte* data, byte* written, nuint cap, nuint* length);

    // Asks once with room for any answer this section gives, and again with
    // the true length where it did not fit.
    private static (int Status, string Text) Answer(string[] texts, Call call)
    {
        Utf8Pack pack = new(texts);
        byte[] room = new byte[RuntimeRemotes.Room];

        while (true)
        {
            nuint length = 0;
            int status;

            fixed (byte* data = pack.Bytes)
            fixed (byte* written = room)
            {
                status = call(pack, data, written, (nuint)room.Length, &length);
            }

            if (length <= (nuint)room.Length)
            {
                string text = Encoding.UTF8.GetString(room, 0, (int)length);
                Array.Clear(room);
                return (status, text);
            }

            Array.Clear(room);
            room = new byte[(int)length];
        }
    }

    private delegate int Lent(
        XmipStr first, XmipStr second, byte* written, nuint cap, nuint* length);

    // Two byte ranges lent for one call, asked again with the true length
    // where the answer did not fit; the answer as the runtime wrote it.
    private static (int Status, byte[] Bytes) Bytes(byte[] first, byte[] second, Lent call)
    {
        byte[] room = new byte[RuntimeRemotes.Room];

        while (true)
        {
            nuint length = 0;
            int status;

            fixed (byte* one = first)
            fixed (byte* two = second)
            fixed (byte* written = room)
            {
                unsafe
                {
                    status = call(
                        new XmipStr(one, (nuint)first.Length),
                        new XmipStr(two, (nuint)second.Length),
                        written, (nuint)room.Length, &length);
                }
            }

            if (length <= (nuint)room.Length)
            {
                return (status, room[..(int)length]);
            }

            room = new byte[(int)length];
        }
    }

    private static string Refusal(int status, string text)
    {
        return status == (int)XmipStatus.Invalid
            ? text
            : $"the runtime answered with {(XmipStatus)status}";
    }
}
