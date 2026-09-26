using System.Text.Json;
using Xmip.Abi.Operate;

namespace Xmip.Abi.Tests;

/// <summary>
/// <see cref="RuntimeCatalogue"/> crosses section 12 of <c>xmip_operate.h</c>
/// to the runtime this estate built. The declarations and their reading are
/// tested in Rust, where they are written (<c>xmip-core</c>'s
/// <c>settings</c> and each technology's own test); these prove the
/// crossing: the answer whole and in the header's shape, and a refusal
/// carried as the runtime's sentence.
/// </summary>
public sealed class RuntimeCatalogueTests
{
    [Fact]
    public void TheCatalogueComesBackAsTheHeadersJson()
    {
        Assert.True(RuntimeRulesTests.Rules.Catalogue.TryRead(null, out string answer), answer);

        using JsonDocument document = JsonDocument.Parse(answer);

        Assert.Equal(
            JsonValueKind.Array,
            document.RootElement.GetProperty("technologies").ValueKind);
    }

    [Fact]
    public void ATechnologyTheRuntimeDoesNotCarryIsRefusedByName()
    {
        const string nowhere = "xmip-core-transport-nowhere";

        Assert.False(RuntimeRulesTests.Rules.Catalogue.TryRead(nowhere, out string refusal));
        Assert.Contains(nowhere, refusal, StringComparison.Ordinal);
    }
}
