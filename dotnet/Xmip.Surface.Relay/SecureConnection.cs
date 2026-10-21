using System.IO.Pipelines;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Relay;

/// <summary>
/// One browser's connection as Kestrel serves it, over a session whose TLS
/// the runtime terminated (section 21): what the browser sends is read on a
/// thread of its own into the connection's input, and what Kestrel writes
/// is sent from another, so the two directions never wait on each other.
/// It carries an <see cref="ITlsConnectionFeature"/> holding the certificate
/// the browser presented, which the runtime verified against the host's
/// anchors, so <c>ConnectionInfo.ClientCertificate</c> and
/// <see cref="ProvenCaller"/> read it as before, and Kestrel's scheme is
/// <c>https</c>.
/// </summary>
internal sealed class SecureConnection
    : ConnectionContext, ITlsConnectionFeature, IConnectionLifetimeFeature
{
    // As much as one read takes off the session at once: a TLS record's worth.
    private const int Chunk = 16 * 1024;

    private readonly SecureSurfaceSession session;

    private readonly Pipe input = new(new PipeOptions(useSynchronizationContext: false));

    private readonly Pipe output = new(new PipeOptions(useSynchronizationContext: false));

    private readonly CancellationTokenSource closed = new();

    private readonly TaskCompletionSource reading =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource writing =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public SecureConnection(SecureSurfaceSession session)
    {
        this.session = session;
        ConnectionId = $"xmip-secure-{Guid.NewGuid():n}";
        Transport = new DuplexPipe(input.Reader, output.Writer);
        LocalEndPoint = Parsed(session.Local);
        RemoteEndPoint = Parsed(session.Remote);
        ClientCertificate = session.Presented.Length == 0
            ? null
            : X509CertificateLoader.LoadCertificate(session.Presented);
        ConnectionClosed = closed.Token;
        Features.Set<ITlsConnectionFeature>(this);
        Features.Set<IConnectionLifetimeFeature>(this);
    }

    /// <inheritdoc />
    public override string ConnectionId { get; set; }

    /// <inheritdoc />
    public override IFeatureCollection Features { get; } = new FeatureCollection();

    /// <inheritdoc />
    public override IDictionary<object, object?> Items { get; set; } =
        new Dictionary<object, object?>();

    /// <inheritdoc />
    public override IDuplexPipe Transport { get; set; }

    /// <summary>The certificate the browser presented, verified by the
    /// runtime against the host's anchors; null where it presented
    /// none.</summary>
    public X509Certificate2? ClientCertificate { get; set; }

    /// <summary>Start both directions, each on a thread of its own.</summary>
    public void Start()
    {
        Pumped(Read, reading, "xmip-secure-read");
        Pumped(Write, writing, "xmip-secure-write");
    }

    /// <inheritdoc />
    public Task<X509Certificate2?> GetClientCertificateAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ClientCertificate);
    }

    /// <inheritdoc />
    public override void Abort(ConnectionAbortedException abortReason)
    {
        session.Close();
        output.Reader.CancelPendingRead();
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        session.Close();
        output.Reader.CancelPendingRead();
        await Task.WhenAll(reading.Task, writing.Task).ConfigureAwait(false);
        session.Dispose();
        ClientCertificate?.Dispose();
        closed.Dispose();
        await base.DisposeAsync().ConfigureAwait(false);
    }

    private static void Pumped(Action pump, TaskCompletionSource done, string name)
    {
        Thread thread = new(() =>
        {
            try
            {
                pump();
            }
            finally
            {
                done.TrySetResult();
            }
        })
        {
            IsBackground = true,
            Name = name,
        };
        thread.Start();
    }

    // What the browser sends, into the connection's input, until it ends or
    // Kestrel reads no more; then the connection is said to be closed.
    private void Read()
    {
        try
        {
            while (true)
            {
                Memory<byte> room = input.Writer.GetMemory(Chunk);
                int read = session.Read(room.Span);

                if (read <= 0)
                {
                    break;
                }

                input.Writer.Advance(read);
                FlushResult flushed = input.Writer.FlushAsync().AsTask().GetAwaiter().GetResult();

                if (flushed.IsCompleted || flushed.IsCanceled)
                {
                    break;
                }
            }
        }
        finally
        {
            input.Writer.Complete();
            closed.Cancel();
        }
    }

    // What Kestrel writes, sent to the browser, until Kestrel writes no more
    // or the browser is gone; then the session is closed.
    private void Write()
    {
        try
        {
            while (true)
            {
                ReadResult taken = output.Reader.ReadAsync().AsTask().GetAwaiter().GetResult();
                bool sent = true;

                foreach (ReadOnlyMemory<byte> segment in taken.Buffer)
                {
                    if (!(sent = session.Write(segment.Span)))
                    {
                        break;
                    }
                }

                output.Reader.AdvanceTo(taken.Buffer.End);

                if (!sent || taken.IsCompleted || taken.IsCanceled)
                {
                    break;
                }
            }
        }
        finally
        {
            output.Reader.Complete();
            session.Close();
        }
    }

    private static IPEndPoint? Parsed(string end)
    {
        return IPEndPoint.TryParse(end, out IPEndPoint? parsed) ? parsed : null;
    }

    private sealed class DuplexPipe(PipeReader input, PipeWriter output) : IDuplexPipe
    {
        public PipeReader Input { get; } = input;

        public PipeWriter Output { get; } = output;
    }
}
