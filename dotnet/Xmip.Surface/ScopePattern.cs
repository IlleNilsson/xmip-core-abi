namespace Xmip.Surface;

/// <summary>
/// A wildcard over scopes, once (ADR-0052 clause 1). ADR-0059 clause 7 is the
/// rule and clause 8 is the syntax: a thing that <em>selects among scopes that
/// already exist</em> takes a wildcard, a thing that <em>names what to create</em>
/// does not, and the wildcard is a wildcard — never a regular expression, so
/// that <c>Rust.Style</c> is the name the operator typed and not a pattern that
/// also catches <c>RustXStyle</c>.
/// </summary>
/// <remarks>
/// <para>
/// PowerShell matches with <c>-like</c> and declares <c>[SupportsWildcards()]</c>;
/// the executable and the GUI match here, so an operator who learns the pattern
/// at one surface is right at the others. Where this cannot be <c>-like</c>
/// exactly, it says so:
/// </para>
/// <list type="bullet">
/// <item><c>*</c> and <c>?</c> are the only metacharacters. <c>-like</c> also
/// reads a character set (<c>[a-c]</c>) and a backtick escape; here <c>[</c>,
/// <c>]</c> and <c>`</c> are literal. No scope the estate publishes carries
/// one, and a set is the part of <c>-like</c> nobody types.</item>
/// <item><c>*</c> crosses a <c>/</c>, as it does in <c>-like</c>, so
/// <c>xmip:///orders/*/receive</c> reaches a stage however deep the node
/// sits.</item>
/// <item>Both sides are read as scopes first — the scheme and the authority
/// drop away (<see cref="ScopeTree.Parts"/>) — so <c>orders/node/edge*</c> and
/// <c>xmip:///orders/node/edge*</c> are one pattern. <c>-like</c> over the raw
/// text would call them two.</item>
/// <item>Case-insensitive and culture-invariant, which is what <c>-like</c> is
/// by default. There is no case-sensitive form here, as there is no surface
/// that asks for one. The <em>scheme</em> is the exception, read the one way
/// <see cref="ScopeTree.Parts"/> reads it — lower-case <c>xmip://</c>, as every
/// publisher writes it — so <c>XMIP:///orders</c> is a path and not a
/// scope.</item>
/// </list>
/// </remarks>
public static class ScopePattern
{
    private static readonly char[] Wildcards = ['*', '?'];

    /// <summary>Whether a pattern asks for a group rather than naming one
    /// scope. A pattern with no wildcard is an exact scope, and every surface
    /// treats it as the scope the operator typed.</summary>
    public static bool HasWildcard(string? pattern)
    {
        return pattern is not null && pattern.IndexOfAny(Wildcards) >= 0;
    }

    /// <summary>
    /// Whether a scope is what a pattern names. A pattern with no wildcard
    /// matches that one scope exactly — never a substring of it and never what
    /// is beneath it; being beneath a scope is <see cref="ScopeTree.Beneath"/>
    /// and is a different question.
    /// </summary>
    public static bool Matches(string candidate, string pattern)
    {
        return Like(Normal(candidate), Normal(pattern));
    }

    /// <summary>A scope as this comparison reads it: the path under the root,
    /// with the scheme, the authority and any trailing slash gone.</summary>
    public static string Normal(string scope)
    {
        return ScopeTree.Join(ScopeTree.Parts(scope));
    }

    /// <summary>
    /// The topmost of a set of scopes: one that lies beneath another in the
    /// same set is dropped. Every command and every view reads a scope
    /// <em>and everything beneath it</em>, so naming both a scope and its
    /// child would say the child twice.
    /// </summary>
    public static IReadOnlyList<string> Topmost(IEnumerable<string> scopes)
    {
        List<string> ordered =
        [
            .. scopes.Select(Normal).Distinct(StringComparer.Ordinal)
                .OrderBy(scope => scope, StringComparer.Ordinal),
        ];
        List<string> topmost = [];

        foreach (string scope in ordered)
        {
            // Against every one kept, not only the last: ordinal order puts
            // `a-x` between `a` and `a/b`, so the last kept is not always the
            // ancestor to ask.
            if (!topmost.Any(kept => ScopeTree.Beneath(scope, kept)))
            {
                topmost.Add(scope);
            }
        }

        return topmost;
    }

    /// <summary>
    /// <c>-like</c> itself: <c>*</c> for any run of characters including none,
    /// <c>?</c> for exactly one, everything else literal, the whole text or
    /// nothing. Iterative with one backtrack point, so a pattern of several
    /// stars over eleven thousand scopes cannot fall off a stack.
    /// </summary>
    private static bool Like(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern)
    {
        int at = 0;
        int step = 0;
        int star = -1;
        int resume = 0;

        while (at < text.Length)
        {
            if (step < pattern.Length && (pattern[step] == '?' || Same(pattern[step], text[at])))
            {
                at++;
                step++;
            }
            else if (step < pattern.Length && pattern[step] == '*')
            {
                star = step++;
                resume = at;
            }
            else if (star >= 0)
            {
                step = star + 1;
                at = ++resume;
            }
            else
            {
                return false;
            }
        }

        while (step < pattern.Length && pattern[step] == '*')
        {
            step++;
        }

        return step == pattern.Length;
    }

    private static bool Same(char pattern, char text)
    {
        return pattern == text || char.ToUpperInvariant(pattern) == char.ToUpperInvariant(text);
    }
}
