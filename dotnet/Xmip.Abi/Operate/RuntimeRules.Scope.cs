using System.Text;
using Xmip.Abi.Module;

namespace Xmip.Abi.Operate;

// Section 7's scope rules: containment, the one wildcard, whether a run is
// shown, a scope's parts and the node and stage it is on.
public sealed partial class RuntimeRules
{
    /// <summary>Whether <paramref name="candidate"/> is <paramref name="scope"/>
    /// or sits beneath it — <c>node::Scope::contains</c>.</summary>
    public bool Contains(string scope, string candidate)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(candidate);

        return Pair(_contains, scope, candidate);
    }

    /// <summary>Whether <paramref name="candidate"/> is what
    /// <paramref name="pattern"/> names by the one wildcard —
    /// <c>node::wildcard::matches</c>.</summary>
    public bool Matches(string candidate, string pattern)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(pattern);

        return Pair(_matches, candidate, pattern);
    }

    /// <summary>Whether what a run made — its cluster, its audit records — is
    /// shown to a reader: always where the run declared nothing, and where it
    /// declared itself <paramref name="hidden"/> only when the reader is
    /// <paramref name="includingHidden"/> — <c>observe::run::shown</c>
    /// (ADR-0028 and ADR-0052, amendments 2026-09-30).</summary>
    public bool Shown(bool hidden, bool includingHidden)
    {
        byte shown = 0;

        unsafe
        {
            Check(_shown((byte)(hidden ? 1 : 0), (byte)(includingHidden ? 1 : 0), &shown));
        }

        return shown != 0;
    }

    // Two strings in, a yes or a no out: the shape containment and the
    // wildcard share.
    private static bool Pair(
        delegate* unmanaged[Cdecl]<XmipStr, XmipStr, byte*, int> call,
        string scope,
        string candidate)
    {
        int scopeMax = Encoding.UTF8.GetMaxByteCount(scope.Length);
        int candidateMax = Encoding.UTF8.GetMaxByteCount(candidate.Length);
        scoped Span<byte> scopeBytes;
        scoped Span<byte> candidateBytes;

        // Left as the stack had it (SkipLocalsInit): only the bytes the
        // encoder writes cross.
        unsafe
        {
            scopeBytes = scopeMax <= Small ? stackalloc byte[Small] : new byte[scopeMax];
            candidateBytes = candidateMax <= Small
                ? stackalloc byte[Small]
                : new byte[candidateMax];
        }

        int scopeLength = Encoding.UTF8.GetBytes(scope, scopeBytes);
        int candidateLength = Encoding.UTF8.GetBytes(candidate, candidateBytes);
        byte contains = 0;

        fixed (byte* scopeData = scopeBytes)
        fixed (byte* candidateData = candidateBytes)
        {
            unsafe
            {
                Check(call(
                    new XmipStr(scopeData, (nuint)scopeLength),
                    new XmipStr(candidateData, (nuint)candidateLength),
                    &contains));
            }
        }

        return contains != 0;
    }

    /// <summary>The segments of a scope's path, top first —
    /// <c>node::Scope::segments</c>. The root has none.</summary>
    public string[] Parts(string scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        int max = Encoding.UTF8.GetMaxByteCount(scope.Length);
        scoped Span<byte> bytes;

        // Left as the stack had it (SkipLocalsInit): only the bytes the encoder
        // writes cross.
        unsafe
        {
            bytes = max <= Small ? stackalloc byte[Small] : new byte[max];
        }

        int length = Encoding.UTF8.GetBytes(scope, bytes);

        fixed (byte* data = bytes)
        {
            XmipStr text;

            unsafe
            {
                text = new(data, (nuint)length);
            }

            const int Deep = 16;
            XmipStr* first = stackalloc XmipStr[Deep];
            nuint count = 0;

            unsafe
            {
                Check(_parts(text, first, Deep, &count));
            }

            // Each part borrows from `bytes`, still pinned here.
            if (count <= Deep)
            {
                string[] parts = new string[count];

                for (int i = 0; i < parts.Length; i++)
                {
                    unsafe
                    {
                        parts[i] = first[i].Read();
                    }
                }

                return parts;
            }

            XmipStr[] found = new XmipStr[count];

            fixed (XmipStr* into = found)
            {
                unsafe
                {
                    Check(_parts(text, into, count, &count));
                }
            }

            unsafe
            {
                return [.. found.Select(part => part.Read())];
            }
        }
    }

    /// <summary>
    /// Where a scope sits — <c>node::Scope::node</c> and
    /// <c>node::Scope::stage</c>: the node it is on, the segment after the
    /// node marker beneath the cluster, and the stage of the message path it is
    /// on beneath that node or, on no node, beneath the cluster. Each is empty
    /// where there is none; the cluster is never a node.
    /// </summary>
    public (string Node, string Stage) Node(string scope)
    {
        using PinnedStr text = XmipStr.Pin(scope);
        XmipStr node;
        XmipStr stage;

        // The node borrows from `text`, still pinned here; the stage is static.
        unsafe
        {
            Check(_node(text.Value, &node, &stage));
            return (node.Read(), stage.Read());
        }
    }
}
