using Xmip.Abi.Module;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// A surface decides from the record and shows the sentence (ADR-0052 clause
/// 4): the verdict carries both, and its Ok is the status, never the words.
/// </summary>
public sealed class ConfigurationVerdictTest
{
    private const string Toml = "edge-01.xmip.toml";

    [Fact]
    public void AValidDocumentIsOkAndSaidSo()
    {
        ValidationRecord answer = new(XmipStatus.Ok, []);

        ConfigurationVerdict verdict = ConfigurationVerdict.Validated(Toml, answer);

        Assert.True(verdict.Ok);
        Assert.Equal(XmipStatus.Ok, verdict.Status);
        Assert.Empty(verdict.Problems);
        Assert.Equal(English.Validated(Toml, answer), verdict.Said);
    }

    [Fact]
    public void AnInvalidDocumentCarriesItsProblems()
    {
        ValidationRecord answer = new(XmipStatus.Invalid, ["no name", "no cluster"]);

        ConfigurationVerdict verdict = ConfigurationVerdict.Validated(Toml, answer);

        Assert.False(verdict.Ok);
        Assert.Equal(["no name", "no cluster"], verdict.Problems);
        Assert.Equal($"{Toml} is invalid: no name; no cluster", verdict.Said);
    }

    [Fact]
    public void AStartIsOkOnlyWhenTheRuntimeSaidOk()
    {
        Assert.True(ConfigurationVerdict.Started(Toml, XmipStatus.Ok).Ok);
        Assert.False(ConfigurationVerdict.Started(Toml, XmipStatus.Invalid).Ok);
        Assert.Equal($"started {Toml}", ConfigurationVerdict.Started(Toml, XmipStatus.Ok).Said);
    }

    [Fact]
    public void NoRuntimeAndNoFileAreVerdictsNotExceptions()
    {
        ConfigurationVerdict unloaded = ConfigurationVerdict.NotLoaded(Toml, "no library");
        ConfigurationVerdict missing = ConfigurationVerdict.NoFile(Toml);

        Assert.Equal(XmipStatus.Unavailable, unloaded.Status);
        Assert.False(unloaded.Ok);
        Assert.Equal("no runtime loaded: no library", unloaded.Said);
        Assert.Equal(XmipStatus.NotFound, missing.Status);
        Assert.False(missing.Ok);
        Assert.Equal($"no configuration at {Toml}", missing.Said);
    }

    [Fact]
    public void AnUnloadedNativeSurfaceAnswersWithAVerdict()
    {
        string nowhere = Path.Combine(Path.GetTempPath(), $"no-runtime-{Guid.NewGuid():n}.dll");
        using NativeOperator surface = new(nowhere);

        ConfigurationVerdict validated = surface.Validate(Toml);
        ConfigurationVerdict started = surface.Start(Toml);

        Assert.Equal(XmipStatus.Unavailable, validated.Status);
        Assert.Equal(XmipStatus.Unavailable, started.Status);
        Assert.StartsWith("no runtime loaded: ", validated.Said, StringComparison.Ordinal);
    }
}
