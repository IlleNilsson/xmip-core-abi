using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// A person's certificate for every .NET surface (ADR-0034, amendment
/// 2026-10-15): a key and a request made into a folder, and a request signed
/// by the authority a node's <c>[provision.operator]</c> names. The files and
/// their names are written here, once, for <c>xmip-cli certificate</c> and
/// <c>New-XmipCertificate</c>; what a request names and what is signed are
/// the runtime's (<c>xmip_operate.h</c> section 19).
/// </summary>
public static class OperatorCertificate
{
    /// <summary>The key's file in the folder a request is made into.</summary>
    public const string KeyFile = "operator.key";

    /// <summary>The request's file beside it.</summary>
    public const string RequestFile = "operator.csr";

    /// <summary>
    /// A fresh key and an operator's request for <paramref name="person"/>,
    /// written into <paramref name="folder"/> as <see cref="KeyFile"/> — on
    /// Unix readable by its owner alone — and <see cref="RequestFile"/>.
    /// Files already there are never overwritten: the request is refused.
    /// </summary>
    public static OperatorRequested Request(
        RuntimeCertificates certificates, string person, string folder)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(folder);

        string key = Path.Combine(folder, KeyFile);
        string request = Path.Combine(folder, RequestFile);

        if (new[] { key, request }.FirstOrDefault(File.Exists) is { } there)
        {
            return new OperatorRequested(
                person, key, request,
                $"REFUSED: {there} is there already; nothing is overwritten.");
        }

        if (!certificates.Request(person, out string keyPem, out string requestPem, out string no))
        {
            return new OperatorRequested(person, key, request, $"REFUSED: {no}");
        }

        Directory.CreateDirectory(folder);
        Write(key, keyPem, secret: true);
        Write(request, requestPem, secret: false);
        return new OperatorRequested(person, key, request, string.Empty);
    }

    /// <summary>
    /// The request in the file <paramref name="request"/>, signed by the
    /// authority the node configuration <paramref name="configuration"/>
    /// names, the chain written to <paramref name="chain"/> where one is
    /// given — never over a file already there.
    /// </summary>
    public static OperatorSigned Sign(
        RuntimeCertificates certificates, string request, string configuration, string? chain)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!File.Exists(request))
        {
            return new OperatorSigned(request, chain, string.Empty, $"No file at {request}.");
        }

        if (chain is not null && File.Exists(chain))
        {
            return new OperatorSigned(
                request, chain, string.Empty,
                $"REFUSED: {chain} is there already; nothing is overwritten.");
        }

        if (!certificates.Sign(
                configuration, File.ReadAllText(request), out string signed, out string no))
        {
            return new OperatorSigned(request, chain, string.Empty, $"REFUSED: {no}");
        }

        if (chain is not null)
        {
            Write(chain, signed, secret: false);
        }

        return new OperatorSigned(request, chain, signed, string.Empty);
    }

    private static void Write(string path, string text, bool secret)
    {
        using FileStream file = new(path, FileMode.CreateNew, FileAccess.Write);

        if (secret && !OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                file.SafeFileHandle, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        using StreamWriter writer = new(file);
        writer.Write(text);
    }
}
