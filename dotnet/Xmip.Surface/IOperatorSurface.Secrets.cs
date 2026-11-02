using Xmip.Abi.Operate;

namespace Xmip.Surface;

// The secrets a node keeps, listed and set through a surface (ADR-0019,
// amendment 2026-10-29).
public partial interface IOperatorSurface
{
    /// <summary>
    /// The secrets the node at <paramref name="node"/> keeps — empty, the
    /// one node this surface reaches — by name, kind and when set, never a
    /// value (ADR-0019, amendment 2026-10-29). Throws
    /// <see cref="InvalidOperationException"/> with the node's words where it
    /// refused or did not answer; the default reaches no node.
    /// </summary>
    public IReadOnlyList<SecretRecord> Secrets(string node, HeldCaller? caller)
    {
        throw new InvalidOperationException($"{Source} reaches no node, so lists no secrets");
    }

    /// <summary>
    /// Set the secret <paramref name="name"/> of <paramref name="kind"/> to
    /// <paramref name="value"/> through the node at <paramref name="node"/>
    /// — empty, the one node this surface reaches — by
    /// <paramref name="who"/>. The node keeps it in the Administration
    /// Database and a Location naming it presents it as it next connects.
    /// Who may act is decided by role, as for every act. The default
    /// declines: this surface reaches no node.
    /// </summary>
    public SecretOperation SetSecret(
        string node, string name, string kind, string value, string who, HeldCaller? caller)
    {
        return SecretOperation.Declined(node, name, $"{Source} reaches no node to set a secret");
    }
}
