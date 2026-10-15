using System.Runtime.InteropServices;

namespace Xmip.Abi.Operate;

/// <summary>
/// The runtime's remote build — <c>--no-default-features --features
/// remote</c>, the phone's (ADR-0014, amendment 2026-10-14) — and the one
/// rule for a library that carries it only: section 7's rules, section 8's
/// publication read, section 9's audit — every surface records its own
/// start and acts — section 17's client, section 18's security roles and
/// section 19's request are always there; what only a node does — serving,
/// the design, the Event hub, the catalogue, process declarations, the acts
/// on this process's nodes and signing a person's request — is bound where
/// the library exports it, and a
/// call into it where it does not is refused in words, never a crash.
/// </summary>
internal static class RemoteBuild
{
    /// <summary>The sentence a call into a node-only binding is refused
    /// with, ahead of the symbol missing.</summary>
    internal const string Sentence = "This runtime library is a remote build";

    /// <summary>Whether <paramref name="library"/> exports every one of
    /// <paramref name="entrypoints"/>.</summary>
    internal static bool Carries(nint library, IReadOnlyList<string> entrypoints)
    {
        return Missing(library, entrypoints) is null;
    }

    /// <summary>The refusal of a call into a binding whose
    /// <paramref name="entrypoints"/> <paramref name="library"/> does not
    /// all export, naming the first it lacks.</summary>
    internal static InvalidOperationException Refused(
        nint library, IReadOnlyList<string> entrypoints)
    {
        string missing = Missing(library, entrypoints) ?? entrypoints[0];

        return new InvalidOperationException(
            $"{Sentence}: it does not export {missing}, which only a node's build carries.");
    }

    private static string? Missing(nint library, IReadOnlyList<string> entrypoints)
    {
        foreach (string entrypoint in entrypoints)
        {
            if (!NativeLibrary.TryGetExport(library, entrypoint, out _))
            {
                return entrypoint;
            }
        }

        return null;
    }
}
