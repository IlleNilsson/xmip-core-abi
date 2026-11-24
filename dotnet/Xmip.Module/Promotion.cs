namespace Xmip.Module;

/// <summary>
/// One opened step of a <see cref="PromoteUnit"/>: what it promotes from a
/// Message's content. A node calls it from any thread, many at once, so it
/// keeps nothing between Messages. Disposed, where it is
/// <see cref="IDisposable"/>, when the node lets the step go.
/// </summary>
public abstract class Promotion
{
    /// <summary>
    /// Read the Message's content and hand each value found to
    /// <paramref name="promoted"/>, under the context property it is promoted
    /// into. A value not found is not handed on.
    /// </summary>
    /// <param name="content">The content, read once, front to back; it may
    /// be larger than memory.</param>
    /// <param name="promoted">Where each value goes; good for this call
    /// alone.</param>
    /// <exception cref="RefusedException">The content is not what the unit
    /// reads; the message says why.</exception>
    public abstract void Promote(Stream content, Promoted promoted);
}
