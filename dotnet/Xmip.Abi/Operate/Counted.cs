namespace Xmip.Abi.Operate;

/// <summary>
/// What a measurement counts. Never a bare number: a Stream at a Receive
/// Location, a Journey in an Xmip Process and a Message at a Send Location are
/// three different quantities, and Xmip keeps those words apart on every page.
/// ADR-0027 clause 5. The values are the header's <c>XMIP_COUNTED_*</c>.
/// </summary>
public enum Counted
{
    /// <summary>Streams, at a Receive Location.</summary>
    Streams = 1,

    /// <summary>Messages, at a Send Location.</summary>
    Messages = 2,

    /// <summary>Journeys, in an Xmip Process.</summary>
    Journeys = 3,

    /// <summary>Bytes, wherever content moved.</summary>
    Bytes = 4,
}
