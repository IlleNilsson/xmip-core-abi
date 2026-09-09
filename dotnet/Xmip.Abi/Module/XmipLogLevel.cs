namespace Xmip.Abi.Module;

/// <summary>The levels a module may log at. Section 6 of the header.</summary>
public enum XmipLogLevel
{
    /// <summary>Something failed.</summary>
    Error = 1,

    /// <summary>Something is wrong and the call went on.</summary>
    Warn = 2,

    /// <summary>What happened.</summary>
    Info = 3,

    /// <summary>Why it happened.</summary>
    Debug = 4,

    /// <summary>Every step of it.</summary>
    Trace = 5,
}
