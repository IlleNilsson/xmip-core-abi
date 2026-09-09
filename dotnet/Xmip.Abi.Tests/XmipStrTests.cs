using Xmip.Abi.Module;

namespace Xmip.Abi.Tests;

/// <summary>A .NET string crosses as UTF-8 and comes back the same.</summary>
public sealed class XmipStrTests
{
    [Fact]
    public void PinnedTextReadsBackTheSame()
    {
        const string text = "xmip:///edge-01/receive/orders — åäö";

        using PinnedStr pinned = XmipStr.Pin(text);

        Assert.Equal(text, pinned.Value.Read());
    }

    [Fact]
    public void EmptyTextIsAnEmptyBorrow()
    {
        // Section 2 of the header: ptr may be null only when len is 0, and a
        // reader treats the two the same.
        using PinnedStr pinned = XmipStr.Pin(string.Empty);

        Assert.Equal((nuint)0, pinned.Value.Len);
        Assert.Equal(string.Empty, pinned.Value.Read());
    }
}
