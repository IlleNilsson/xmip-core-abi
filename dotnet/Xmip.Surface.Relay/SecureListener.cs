using System.Net;
using System.Threading.Channels;
using Microsoft.AspNetCore.Connections;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Relay;

/// <summary>
/// Kestrel's listener over the runtime's HTTPS listener (section 21): one
/// thread of its own takes each connection whose handshake the runtime
/// finished and hands it to Kestrel's accept. A browser whose certificate
/// the anchors refused is told to <c>refused</c> — the host's audit — and
/// one that did not finish for any other reason, a probe that connects and
/// hangs up among them, is passed over; neither ends the listener.
/// </summary>
internal sealed class SecureListener : IConnectionListener
{
    private readonly SecureSurfaceListener listener;

    private readonly Action<string>? refused;

    private readonly Channel<SecureSurfaceSession> finished =
        Channel.CreateUnbounded<SecureSurfaceSession>(
            new UnboundedChannelOptions { SingleWriter = true });

    private readonly TaskCompletionSource accepting =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public SecureListener(
        SecureSurfaceListener listener, SecureEndPoint bound, Action<string>? refused)
    {
        this.listener = listener;
        this.refused = refused;
        EndPoint = bound;

        Thread thread = new(Accepting) { IsBackground = true, Name = "xmip-secure-accept" };
        thread.Start();
    }

    /// <inheritdoc />
    public EndPoint EndPoint { get; }

    /// <inheritdoc />
    public async ValueTask<ConnectionContext?> AcceptAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            SecureSurfaceSession session =
                await finished.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            SecureConnection connection = new(session);
            connection.Start();
            return connection;
        }
        catch (ChannelClosedException)
        {
            // Unbound: Kestrel's accept loop ends on null.
            return null;
        }
    }

    /// <inheritdoc />
    public ValueTask UnbindAsync(CancellationToken cancellationToken = default)
    {
        listener.Stop();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        listener.Stop();
        await accepting.Task.ConfigureAwait(false);
        listener.Dispose();

        // A session finished and never taken is closed here.
        while (finished.Reader.TryRead(out SecureSurfaceSession? left))
        {
            left.Dispose();
        }
    }

    private void Accepting()
    {
        try
        {
            while (true)
            {
                SecureSurfaceAccepted accepted = listener.Accept();

                if (accepted.Stopped)
                {
                    return;
                }

                if (accepted.Session is { } session)
                {
                    if (!finished.Writer.TryWrite(session))
                    {
                        session.Dispose();
                    }
                }
                else if (accepted.Refused)
                {
                    refused?.Invoke(accepted.Said);
                }
            }
        }
        finally
        {
            finished.Writer.TryComplete();
            accepting.TrySetResult();
        }
    }
}
