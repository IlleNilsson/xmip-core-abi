namespace Xmip.Abi.Operate;

/// <summary>
/// How a listener's connections are protected (section 21): a parameter,
/// never a name — the owner, 2026-10-21, <i>technology and security should
/// not be in functions, parameters is ok</i>. The values are the header's
/// <c>XMIP_PROTECTION_*</c>.
/// </summary>
public enum Protection
{
    /// <summary><c>XMIP_PROTECTION_TLS</c>: <c>xmip-core-library-tls</c>,
    /// the one built.</summary>
    Tls = 1,
}
