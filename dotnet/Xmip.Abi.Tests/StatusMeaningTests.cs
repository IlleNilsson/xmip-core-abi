using Xmip.Abi.Module;

namespace Xmip.Abi.Tests;

/// <summary>
/// What a status code means, said once for <c>xmip-cli status</c> and
/// <c>ConvertFrom-XmipStatus</c> alike. The surfaces render it; the rule is
/// held here.
/// </summary>
public sealed class StatusMeaningTests
{
    [Fact]
    public void AKnownCodeCarriesTheHeadersNameAndTheBindingsEnglish()
    {
        StatusMeaning timeout = StatusMeaning.Of((int)XmipStatus.Timeout);

        Assert.True(timeout.Known);
        Assert.Equal("Timeout", timeout.Name);
        Assert.Equal(XmipStatus.Timeout.Explain(), timeout.Meaning);
        Assert.True(timeout.Retryable);
        Assert.False(timeout.Terminal);
    }

    [Fact]
    public void ATerminalCodeIsNeverRetryable()
    {
        StatusMeaning panic = StatusMeaning.Of((int)XmipStatus.Panic);

        Assert.True(panic.Terminal);
        Assert.False(panic.Retryable);
    }

    [Fact]
    public void ACodeTheHeaderDoesNotDefineIsUnknownInLowerCaseAndNothingElse()
    {
        // Until 2026-09-24 the cli said unknown and the cmdlet Unknown.
        StatusMeaning stranger = StatusMeaning.Of(4711);

        Assert.False(stranger.Known);
        Assert.Equal(StatusMeaning.Unknown, stranger.Name);
        Assert.Equal("unknown", stranger.Name);
        Assert.Equal(4711, stranger.Code);
        Assert.False(stranger.Retryable);
        Assert.False(stranger.Terminal);
        Assert.Equal("not a status this build knows", stranger.Meaning);
    }
}
