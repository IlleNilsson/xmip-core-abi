namespace Xmip.Surface.Test;

/// <summary>
/// The one wildcard every surface matches with (ADR-0059 clauses 7 and 8, read
/// for the surfaces that are not PowerShell). What is asserted here is what
/// <c>-like</c> does, because an operator who learns the pattern at the prompt
/// must be right at the command line and on the page — and, where this cannot
/// be <c>-like</c>, what it does instead.
/// </summary>
public sealed class ScopePatternTest
{
    [Fact]
    public void APatternWithNoWildcardIsTheScopeItself()
    {
        Assert.False(ScopePattern.HasWildcard("xmip:///C1/node/alpha"));
        Assert.True(ScopePattern.Matches("xmip:///C1/node/alpha", "xmip:///C1/node/alpha"));

        // Not a substring, either way about: exactness is the whole point of a
        // parameter that takes no wildcard.
        Assert.False(ScopePattern.Matches("xmip:///C1/node/alpha", "node"));
        Assert.False(ScopePattern.Matches("xmip:///C1/node/alpha", "xmip:///C1/node"));
        Assert.False(ScopePattern.Matches("xmip:///C1/node/alpha", "xmip:///C1/node/al"));
        Assert.False(ScopePattern.Matches("xmip:///C1/node/alphabet", "xmip:///C1/node/alpha"));
    }

    /// <summary>
    /// ADR-0059 clause 8: <c>Rust.Style</c> is a real test name, and as a
    /// regular expression it would also catch <c>RustXStyle</c>. A wildcard
    /// reads the dot as the operator typed it.
    /// </summary>
    [Fact]
    public void ADotIsADotAndNotAnyCharacter()
    {
        const string scope = "xmip:///C1/node/alpha/test/Rust.Style";

        Assert.True(ScopePattern.Matches(scope, "xmip:///C1/node/alpha/test/Rust.Style"));
        Assert.False(
            ScopePattern.Matches("xmip:///C1/node/alpha/test/RustXStyle",
                "xmip:///C1/node/alpha/test/Rust.Style"));
        Assert.True(ScopePattern.Matches(scope, "*/Rust.*"));
        Assert.False(ScopePattern.Matches("xmip:///C1/node/alpha/test/RustXStyle", "*/Rust.*"));
    }

    [Fact]
    public void AStarIsAnyRunAndAQuestionMarkIsExactlyOne()
    {
        Assert.True(ScopePattern.HasWildcard("xmip:///C1/node/al*"));
        Assert.True(ScopePattern.Matches("xmip:///C1/node/alpha", "xmip:///C1/node/al*"));
        Assert.True(ScopePattern.Matches("xmip:///C1/node/alpha", "xmip:///C1/node/alph?"));
        Assert.False(ScopePattern.Matches("xmip:///C1/node/alphas", "xmip:///C1/node/alph?"));
        Assert.True(ScopePattern.Matches("xmip:///C1/node/alpha", "*"));
        Assert.True(ScopePattern.Matches(ScopeTree.Root, "*"));

        // A star matches nothing at all, as it does in -like.
        Assert.True(ScopePattern.Matches("xmip:///C1", "xmip:///C1*"));
    }

    [Fact]
    public void AStarCrossesASlashAsItDoesInLike()
    {
        Assert.True(
            ScopePattern.Matches("xmip:///C1/node/alpha/receive", "xmip:///C1/*/receive"));
        Assert.True(ScopePattern.Matches("xmip:///C1/node/alpha/receive/tcp", "xmip:///C1/*"));
        Assert.False(
            ScopePattern.Matches("xmip:///C1/node/alpha/receive/tcp", "xmip:///C1/*/receive"));
    }

    [Fact]
    public void MatchingIsCaseInsensitiveAndInvariant()
    {
        Assert.True(ScopePattern.Matches("xmip:///C1/node/alpha", "xmip:///c1/NODE/AL*"));
        Assert.True(ScopePattern.Matches("xmip:///C1/node/alpha", "C1/NODE/alpha"));

        // The segments are case-insensitive; the scheme is read the one way
        // ScopeTree reads it, lower case, as every publisher writes it.
        Assert.False(ScopePattern.Matches("xmip:///C1/node/alpha", "XMIP:///C1/node/alpha"));
    }

    /// <summary>
    /// A scope is read as a scope on both sides before the comparison, so the
    /// scheme, the authority and a trailing slash cannot make two spellings of
    /// one pattern. That is where this departs from <c>-like</c> over raw text,
    /// and it departs on purpose.
    /// </summary>
    [Fact]
    public void BothSidesAreReadAsScopesFirst()
    {
        Assert.True(ScopePattern.Matches("xmip:///C1/node/alpha", "C1/node/al*"));
        Assert.True(
            ScopePattern.Matches("xmip://host:9000/C1/node/alpha", "xmip:///C1/node/alpha"));
        Assert.True(ScopePattern.Matches("xmip:///C1/node/alpha/", "xmip:///C1/node/alpha"));
        Assert.Equal("xmip:///C1/node/alpha", ScopePattern.Normal("xmip:///C1/node/alpha/"));
    }

    /// <summary>
    /// The documented departure: <c>-like</c> reads <c>[a-c]</c> as a set and a
    /// backtick as an escape, and this does not. Both are literal here, and no
    /// scope the estate publishes carries either.
    /// </summary>
    [Fact]
    public void ACharacterSetIsLiteralAndIsNotASet()
    {
        Assert.False(ScopePattern.Matches("xmip:///C1/node/alpha", "xmip:///C1/node/[ab]lpha"));
        Assert.True(ScopePattern.Matches("xmip:///C1/node/[ab]lpha", "xmip:///C1/node/[ab]lpha"));
    }

    [Fact]
    public void APatternThatMatchesNothingMatchesNothing()
    {
        string[] scopes =
        [
            "xmip:///C1", "xmip:///C1/node/alpha", "xmip:///C1/node/beta",
            "xmip:///C1/node/gamma",
        ];

        Assert.DoesNotContain(scopes, scope => ScopePattern.Matches(scope, "xmip:///C1/node/Q*"));
        Assert.DoesNotContain(scopes, scope => ScopePattern.Matches(scope, "xmip:///C2*"));
    }

    [Fact]
    public void TheTopmostDropWhatIsAlreadyBeneathAnother()
    {
        string[] matched =
        [
            "xmip:///C1/node/alpha/receive",
            "xmip:///C1/node/alpha",
            "xmip:///C1/node/alpha/receive/tcp",
            "xmip:///C1/node/beta",
        ];

        Assert.Equal(
            ["xmip:///C1/node/alpha", "xmip:///C1/node/beta"],
            ScopePattern.Topmost(matched));
    }

    /// <summary>
    /// Ordinal order puts <c>alpha-spare</c> between <c>alpha</c> and <c>alpha/receive</c>,
    /// so the topmost cannot be decided against the last one kept alone. This is
    /// the case that would have gone unnoticed.
    /// </summary>
    [Fact]
    public void TheTopmostAreDecidedAgainstEveryOneKept()
    {
        string[] matched =
            ["xmip:///C1/alpha", "xmip:///C1/alpha-spare", "xmip:///C1/alpha/receive"];

        Assert.Equal(["xmip:///C1/alpha", "xmip:///C1/alpha-spare"], ScopePattern.Topmost(matched));
    }

    [Fact]
    public void TheRootSwallowsEverythingBeneathIt()
    {
        Assert.Equal(
            [ScopeTree.Root],
            ScopePattern.Topmost([ScopeTree.Root, "xmip:///C1", "xmip:///C1/node/alpha"]));
    }
}
