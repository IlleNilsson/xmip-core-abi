using Microsoft.Extensions.Configuration;

namespace Xmip.Surface;

/// <summary>
/// The one rule a host grants a caller a role by (ADR-0009, amendments
/// 2026-09-14 and 2026-10-06), for the web, the desktop and the surface hub
/// alike: a caller nothing proved is an Observer and acts on nothing; a role
/// the run states is every proven caller's; else the directory the host was
/// handed answers for the caller; else Observer, so an unstated role grants
/// nothing. The Playground's permissive identity is its fake directory,
/// <see cref="TesterDirectory"/>, there only where configuration names it.
/// </summary>
/// <param name="stated">The role the run states, or null.</param>
/// <param name="directory">The directory asked where the run states none, or null.</param>
public sealed class RoleAssignment(Role? stated, IDirectory? directory)
{
    /// <summary>The configuration key a run states a role under, in the host's
    /// <c>[Xmip]</c> table.</summary>
    public const string ConfigurationKey = "Xmip:Role";

    /// <summary>The environment variable a run states a role in.</summary>
    public const string EnvironmentVariable = "XMIP_ROLE";

    /// <summary>The configuration key naming the directory's kind.</summary>
    public const string DirectoryKey = "Xmip:Directory";

    /// <summary>
    /// The assignment a host's configuration states: <see cref="ConfigurationKey"/>,
    /// else <see cref="EnvironmentVariable"/>, a word that is no role being
    /// Observer; and the directory <see cref="DirectoryKey"/> names, of which
    /// the one built is <see cref="TesterDirectory.Kind"/>, allowing the
    /// operating system user the host runs as. A kind not built is no
    /// directory, so a misstatement grants nothing.
    /// </summary>
    public static RoleAssignment From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? said = configuration[ConfigurationKey]
            ?? Environment.GetEnvironmentVariable(EnvironmentVariable);
        IDirectory? asked = string.Equals(
                configuration[DirectoryKey]?.Trim(),
                TesterDirectory.Kind,
                StringComparison.OrdinalIgnoreCase)
            ? new TesterDirectory(GatedOperator.HostUser)
            : null;

        return new RoleAssignment(string.IsNullOrWhiteSpace(said) ? null : Parse(said), asked);
    }

    /// <summary>
    /// The role named in configuration, or Observer when unrecognised — the
    /// safe default, so a misconfiguration never grants privilege.
    /// </summary>
    public static Role Parse(string? text)
    {
        return text?.Trim().ToLowerInvariant() switch
        {
            "operator" => Role.Operator,
            "developer" => Role.Developer,
            _ => Role.Observer,
        };
    }

    /// <summary>The caller <paramref name="who"/> — the identity a connection
    /// proved, or null — and the role this assignment grants them.</summary>
    public RoleContext For(string? who)
    {
        Role role = who is null
            ? Role.Observer
            : stated ?? directory?.RoleOf(who) ?? Role.Observer;

        return new RoleContext(role, who);
    }
}
