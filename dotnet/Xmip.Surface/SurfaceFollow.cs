using System.Runtime.CompilerServices;

namespace Xmip.Surface;

/// <summary>
/// Following a selection as its publisher advances, once for every surface
/// that follows (ADR-0014 clause 10): the answer for the current snapshot,
/// then a new answer whenever the surface says its published snapshot
/// advanced and the answer changed, until the token is cancelled. A wildcard
/// is matched again at every notice, so a scope that appears is followed and
/// one that goes leaves the answer rather than the operator's memory; a
/// pattern that now names nothing answers for no scope, since the refusal
/// belongs to a command that ends. Notices may be coalesced; each answer is
/// the latest truth.
/// </summary>
/// <remarks>
/// What an answer is stays the face's: <c>xmip-cli --follow</c> writes a JSON
/// Lines document, <c>Get-XmipHealth -Follow</c> and <c>Get-XmipScope
/// -Follow</c> write the objects. The loop, the rematch and the "changed"
/// test are here, so the faces cannot follow differently. Cancelling ends the
/// sequence with <see cref="OperationCanceledException"/>, the normal end of a
/// follow, which the face treats as such.
/// </remarks>
public static class SurfaceFollow
{
    /// <summary>Each answer <paramref name="read"/> gives for what
    /// <paramref name="chosen"/> names now, whenever it differs from the last
    /// by <paramref name="same"/> (value equality when omitted).</summary>
    public static async IAsyncEnumerable<T> Changes<T>(
        IOperatorSurface surface,
        ScopeSelection chosen,
        Func<IOperatorSurface, ScopeSelection, T> read,
        IEqualityComparer<T>? same = null,
        [EnumeratorCancellation] CancellationToken stop = default)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(read);

        IEqualityComparer<T> equal = same ?? EqualityComparer<T>.Default;
        bool said = false;
        T last = default!;

        await foreach (SurfaceChange notice in surface.WatchAsync(stop).ConfigureAwait(false))
        {
            ScopeSelection now = ScopeSelection.Of(surface, chosen.Argument, out _)
                ?? chosen with { Scopes = [] };
            T answer = read(surface, now);

            if (said && equal.Equals(answer, last))
            {
                continue;
            }

            said = true;
            last = answer;

            yield return answer;
        }
    }

    /// <summary>Two answers of rows as the same answer when they hold equal
    /// rows in the same order: what a face that writes objects compares.</summary>
    public static IEqualityComparer<IReadOnlyList<T>> Rows<T>()
    {
        return RowComparer<T>.Instance;
    }

    private sealed class RowComparer<T> : IEqualityComparer<IReadOnlyList<T>>
    {
        public static RowComparer<T> Instance { get; } = new();

        public bool Equals(IReadOnlyList<T>? x, IReadOnlyList<T>? y)
        {
            return ReferenceEquals(x, y) || (x is not null && y is not null && x.SequenceEqual(y));
        }

        public int GetHashCode(IReadOnlyList<T> obj)
        {
            return obj.Count;
        }
    }
}
