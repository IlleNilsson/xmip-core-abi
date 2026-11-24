using Xmip.Abi.Module;

namespace Xmip.Module;

/// <summary>
/// A Message's content as a read-only <see cref="Stream"/> over the node's
/// <c>XmipReader</c> (<c>xmip_module.h</c> section 5): a piece at a time, so
/// content larger than memory passes. Good for one call alone.
/// </summary>
internal sealed class ContentStream : Stream
{
    private XmipReader* _reader;

    internal ContentStream(XmipReader* reader)
    {
        _reader = reader;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        XmipReader* reader = _reader;

        if (reader is null)
        {
            throw new ObjectDisposedException(nameof(ContentStream));
        }

        if (buffer.IsEmpty)
        {
            return 0;
        }

        long read;

        unsafe
        {
            fixed (byte* into = buffer)
            {
                read = reader->Read(reader->Ctx, into, (nuint)buffer.Length);
            }
        }

        return read >= 0
            ? checked((int)read)
            : throw new BoundaryException((int)read, "the node's content could not be read");
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _reader = null;
        base.Dispose(disposing);
    }
}
