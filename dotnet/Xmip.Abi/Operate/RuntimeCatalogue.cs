using System.Runtime.InteropServices;
using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

/// <summary>
/// Section 12 of <c>include/xmip_operate.h</c>, crossed by P/Invoke: the
/// technologies the runtime carries and the settings each declares
/// (ADR-0064, amendment 2026-09-26). Every technology declares its own
/// settings in its own crate and the runtime's library answers them; this
/// binds the one call once for every .NET surface, so the desktop editor
/// builds a Location's form from the same answer the language server reads.
/// </summary>
/// <remarks>
/// Pure in the header's sense: it reads no snapshot and holds nothing, so one
/// instance serves a whole process from any thread. The answer is the
/// header's JSON, in memory only (ADR-0031 clause 2).
/// </remarks>
public sealed unsafe class RuntimeCatalogue
{
    // What a catalogue is in practice, so one call answers; a longer one is
    // asked for again at its true length.
    private const int Room = 64 * 1024;

    private readonly delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int> _catalogue;

    internal RuntimeCatalogue(nint library)
    {
        _catalogue = (delegate* unmanaged[Cdecl]<XmipStr, byte*, nuint, nuint*, int>)
            NativeLibrary.GetExport(library, OperateAbi.TechnologyCatalogueEntrypoint);
    }

    /// <summary>Section 12's symbol, which a runtime must export.</summary>
    public static IReadOnlyList<string> Entrypoints { get; } =
        [OperateAbi.TechnologyCatalogueEntrypoint];

    /// <summary>
    /// The technologies the runtime carries, each with its capability and its
    /// settings, as the header's JSON — or the one <paramref name="technology"/>
    /// names alone. False, with the runtime's sentence in
    /// <paramref name="answer"/>, when it carries no technology of that name.
    /// </summary>
    public bool TryRead(string? technology, out string answer)
    {
        byte[] name = Encoding.UTF8.GetBytes(technology ?? string.Empty);
        byte[] text = new byte[Room];
        nuint needed = 0;
        int status = Call(name, text, ref needed);

        if (needed > (nuint)text.Length)
        {
            text = new byte[(int)needed];
            status = Call(name, text, ref needed);
        }

        answer = Encoding.UTF8.GetString(text, 0, (int)Math.Min(needed, (nuint)text.Length));

        return status switch
        {
            0 => true,
            (int)XmipStatus.Invalid => false,
            _ => throw new InvalidOperationException(
                $"the runtime answered the technology catalogue with {(XmipStatus)status}"),
        };
    }

    private int Call(byte[] name, byte[] text, ref nuint needed)
    {
        fixed (byte* nameData = name)
        fixed (byte* textData = text)
        fixed (nuint* length = &needed)
        {
            return _catalogue(
                new XmipStr(nameData, (nuint)name.Length), textData, (nuint)text.Length, length);
        }
    }
}
