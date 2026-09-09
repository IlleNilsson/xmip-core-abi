using System.Globalization;
using System.Text.RegularExpressions;

namespace Xmip.Abi.Tests;

/// <summary>
/// Reading the normative headers, for the tests that compare against them.
/// The same reading xmip-core-powershell's HeaderStatus.ps1 does, so that a
/// binding and a surface disagree with the header in the same test.
/// </summary>
internal static class Header
{
    /// <summary><c>#define XMIP_OK 0</c>, <c>#define XMIP_E_INVALID (-1)</c>.</summary>
    private static readonly Regex Status = new(
        @"^#define\s+XMIP_(OK|E_[A-Z_]+)\s+\(?(-?\d+)\)?",
        RegexOptions.Multiline);

    /// <summary><c>XMIP_HEALTH_FINE = 0,</c> and the like, inside an enum.</summary>
    private static readonly Regex Enumerator = new(
        @"^\s*XMIP_([A-Z]+)_([A-Z]+)\s*=\s*(\d+)",
        RegexOptions.Multiline);

    /// <summary>The header's text, from beside the test assembly.</summary>
    public static string Text(string name)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "include", name));
    }

    /// <summary>Every status the module header defines, name to value.</summary>
    public static IReadOnlyDictionary<string, int> Statuses()
    {
        Dictionary<string, int> found = [];

        foreach (Match match in Status.Matches(Text("xmip_module.h")))
        {
            found[match.Groups[1].Value] =
                int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        }

        return found;
    }

    /// <summary>Every enumerator <c>XMIP_&lt;family&gt;_NAME = value</c> in the
    /// operate header, for one family such as <c>HEALTH</c>.</summary>
    public static IReadOnlyDictionary<string, int> Enumerators(string family)
    {
        Dictionary<string, int> found = [];

        foreach (Match match in Enumerator.Matches(Text("xmip_operate.h")))
        {
            if (string.Equals(match.Groups[1].Value, family, StringComparison.Ordinal))
            {
                found[match.Groups[2].Value] =
                    int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            }
        }

        return found;
    }

    /// <summary>The value of one <c>#define NAME value</c>, with a trailing
    /// <c>u</c> or quotes removed.</summary>
    public static string Define(string header, string name)
    {
        Match match = Regex.Match(
            Text(header),
            @"^#define\s+" + Regex.Escape(name) + @"\s+(\S+)",
            RegexOptions.Multiline);

        Assert.True(match.Success, $"{name} is not in {header}");

        return match.Groups[1].Value.TrimEnd('u').Trim('"');
    }

    /// <summary>
    /// The C# member name a header constant corresponds to. <c>OK</c> is
    /// <c>Ok</c>; <c>E_NOT_FOUND</c> is <c>NotFound</c>. The prefix and the
    /// underscores go and what is left is Pascal case. That is the whole
    /// naming rule, and stating it as code is what lets the comparison be a
    /// test rather than a table somebody maintains.
    /// </summary>
    public static string MemberName(string constant)
    {
        string bare = constant.StartsWith("E_", StringComparison.Ordinal)
            ? constant[2..]
            : constant;

        IEnumerable<string> words = bare
            .Split('_')
            .Select(word => word[..1].ToUpperInvariant() + word[1..].ToLowerInvariant());

        return string.Concat(words);
    }
}
