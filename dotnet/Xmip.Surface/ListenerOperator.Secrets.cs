using Xmip.Abi.Module;

namespace Xmip.Surface;

/// <summary>A secret set through the node a listener reaches, and the secrets
/// it keeps listed, never a value (ADR-0019, amendment 2026-10-29).</summary>
public abstract partial class ListenerOperator
{
    /// <inheritdoc />
    public IReadOnlyList<SecretRecord> Secrets(string node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (this.node is null)
        {
            throw new InvalidOperationException(Source);
        }

        XmipStatus status = this.node.Secrets(node, out string text);

        if (status != XmipStatus.Ok)
        {
            throw new InvalidOperationException(text);
        }

        return SecretRecord.Listed(text);
    }

    /// <inheritdoc />
    public SecretOperation SetSecret(
        string node, string name, string kind, string value, string who)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(name);

        if (this.node is null)
        {
            return SecretOperation.Declined(node, name, Source);
        }

        XmipStatus status = this.node.SetSecret(node, name, kind, value, out string said);

        if (status == XmipStatus.Unavailable)
        {
            IsConnected = false;
            Reason = said;
        }

        return new SecretOperation(node, name, status == XmipStatus.Ok, said);
    }
}
