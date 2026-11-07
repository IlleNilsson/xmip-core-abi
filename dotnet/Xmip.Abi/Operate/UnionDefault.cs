namespace Xmip.Abi.Operate;

/// <summary>
/// The one answer to a union's default value, which holds none of its cases
/// and which nothing in Xmip makes. A switch over one of Xmip's unions names
/// every case and then this one, as <c>null</c>: populated, never a discard.
/// </summary>
public static class UnionDefault
{
    /// <summary>The refusal a switch throws for a default
    /// <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The union.</typeparam>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException Of<T>()
        where T : struct
    {
        return new InvalidOperationException($"A default {typeof(T).Name} holds no case.");
    }
}
