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
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The cluster, two of its nodes by what each declares, and the receiving
    // one's scope: the patterns below are built from these names.
    private static readonly string Name = Cluster.Name;
    private static readonly string Receiver = Cluster.WithRole("receiving");
    private static readonly string Processor = Cluster.WithRole("processing");
    private static readonly string Nodes = $"{Cluster.Scope}/node";
    private static readonly string Node = $"{Nodes}/{Receiver}";

    [Fact]
    public void APatternWithNoWildcardIsTheScopeItself()
    {
        Assert.False(ScopePattern.HasWildcard(Node));
        Assert.True(ScopePattern.Matches(Node, Node));

        // Not a substring, either way about: exactness is the whole point of a
        // parameter that takes no wildcard.
        Assert.False(ScopePattern.Matches(Node, "node"));
        Assert.False(ScopePattern.Matches(Node, Nodes));
        Assert.False(ScopePattern.Matches(Node, $"{Nodes}/{Receiver[..^1]}"));
        Assert.False(ScopePattern.Matches($"{Node}bet", Node));
    }

    /// <summary>
    /// ADR-0059 clause 8: <c>Rust.Style</c> is a real test name, and as a
    /// regular expression it would also catch <c>RustXStyle</c>. A wildcard
    /// reads the dot as the operator typed it.
    /// </summary>
    [Fact]
    public void ADotIsADotAndNotAnyCharacter()
    {
        string scope = $"{Node}/test/Rust.Style";
        string other = $"{Node}/test/RustXStyle";

        Assert.True(ScopePattern.Matches(scope, scope));
        Assert.False(ScopePattern.Matches(other, scope));
        Assert.True(ScopePattern.Matches(scope, "*/Rust.*"));
        Assert.False(ScopePattern.Matches(other, "*/Rust.*"));
    }

    [Fact]
    public void AStarIsAnyRunAndAQuestionMarkIsExactlyOne()
    {
        string start = $"{Nodes}/{Receiver[..1]}*";
        string oneMore = $"{Nodes}/{Receiver[..^1]}?";

        Assert.True(ScopePattern.HasWildcard(start));
        Assert.True(ScopePattern.Matches(Node, start));
        Assert.True(ScopePattern.Matches(Node, oneMore));
        Assert.False(ScopePattern.Matches($"{Node}s", oneMore));
        Assert.True(ScopePattern.Matches(Node, "*"));
        Assert.True(ScopePattern.Matches(ScopeTree.Root, "*"));

        // A star matches nothing at all, as it does in -like.
        Assert.True(ScopePattern.Matches(Cluster.Scope, $"{Cluster.Scope}*"));
    }

    [Fact]
    public void AStarCrossesASlashAsItDoesInLike()
    {
        string stage = $"{Cluster.Scope}/*/receive";

        Assert.True(ScopePattern.Matches($"{Node}/receive", stage));
        Assert.True(ScopePattern.Matches($"{Node}/receive/tcp", $"{Cluster.Scope}/*"));
        Assert.False(ScopePattern.Matches($"{Node}/receive/tcp", stage));
    }

    [Fact]
    public void MatchingIsCaseInsensitiveAndInvariant()
    {
        string lower = Name.ToLowerInvariant();
        string upper = Receiver.ToUpperInvariant();

        Assert.True(ScopePattern.Matches(Node, $"xmip:///{lower}/NODE/{upper[..1]}*"));
        Assert.True(ScopePattern.Matches(Node, $"{Name}/NODE/{Receiver}"));

        // The segments are case-insensitive; the scheme is read the one way
        // ScopeTree reads it, lower case, as every publisher writes it.
        Assert.False(ScopePattern.Matches(Node, $"XMIP:///{Name}/node/{Receiver}"));
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
        Assert.True(ScopePattern.Matches(Node, $"{Name}/node/{Receiver[..1]}*"));
        Assert.True(
            ScopePattern.Matches($"xmip://host:9000/{Name}/node/{Receiver}", Node));
        Assert.True(ScopePattern.Matches($"{Node}/", Node));
        Assert.Equal(Node, ScopePattern.Normal($"{Node}/"));
    }

    /// <summary>
    /// The documented departure: <c>-like</c> reads <c>[a-c]</c> as a set and a
    /// backtick as an escape, and this does not. Both are literal here, and no
    /// scope the estate publishes carries either.
    /// </summary>
    [Fact]
    public void ACharacterSetIsLiteralAndIsNotASet()
    {
        string set = $"{Nodes}/[{Receiver[..1]}x]{Receiver[1..]}";

        Assert.False(ScopePattern.Matches(Node, set));
        Assert.True(ScopePattern.Matches(set, set));
    }

    [Fact]
    public void APatternThatMatchesNothingMatchesNothing()
    {
        string[] scopes =
        [
            Cluster.Scope, .. Cluster.Nodes.Select(node => $"{Nodes}/{node}"),
        ];

        Assert.DoesNotContain(
            scopes, scope => ScopePattern.Matches(scope, $"{Node}-absent*"));
        Assert.DoesNotContain(
            scopes, scope => ScopePattern.Matches(scope, $"{Cluster.Scope}-absent*"));
    }

    [Fact]
    public void TheTopmostDropWhatIsAlreadyBeneathAnother()
    {
        string other = $"{Nodes}/{Processor}";
        string[] matched = [$"{Node}/receive", Node, $"{Node}/receive/tcp", other];

        Assert.Equal(
            new[] { Node, other }.Order(StringComparer.Ordinal),
            ScopePattern.Topmost(matched));
    }

    /// <summary>
    /// Ordinal order puts <c>&lt;node&gt;-spare</c> between <c>&lt;node&gt;</c>
    /// and <c>&lt;node&gt;/receive</c>, so the topmost cannot be decided against
    /// the last one kept alone. This is the case that would have gone unnoticed.
    /// </summary>
    [Fact]
    public void TheTopmostAreDecidedAgainstEveryOneKept()
    {
        string node = $"{Cluster.Scope}/{Receiver}";
        string[] matched = [node, $"{node}-spare", $"{node}/receive"];

        Assert.Equal([node, $"{node}-spare"], ScopePattern.Topmost(matched));
    }

    [Fact]
    public void TheRootSwallowsEverythingBeneathIt()
    {
        Assert.Equal(
            [ScopeTree.Root],
            ScopePattern.Topmost([ScopeTree.Root, Cluster.Scope, Node]));
    }
}
