namespace Xmip.Abi.Module;

/// <summary>
/// What a value is. <c>XmipValueKind</c>, section 10 of
/// <c>xmip_module.h</c>: a message tree's node, and the kind a Promote unit
/// hands each value it promotes in (section 14).
/// </summary>
public enum XmipValueKind
{
    /// <summary>No value.</summary>
    Null = 0,

    /// <summary>A boolean, written <c>true</c> or <c>false</c>.</summary>
    Bool = 1,

    /// <summary>A number, written as its text.</summary>
    Number = 2,

    /// <summary>Text, UTF-8.</summary>
    String = 3,

    /// <summary>Bytes.</summary>
    Binary = 4,

    /// <summary>An ordered run of values.</summary>
    Sequence = 5,

    /// <summary>Values by name.</summary>
    Map = 6,
}
