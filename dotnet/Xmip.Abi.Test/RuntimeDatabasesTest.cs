using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeDatabases"/> crosses section 20 to the runtime this
/// estate built and back (open problem 41; ADR-0015, amendment 2026-10-20).
/// Until the first release the schema is version 1, so there is no upgrade
/// to write: the runtime refuses one in words, and a server Xmip Storage
/// keeps no database on likewise. What an upgrade writes is
/// <c>xmip-core-persist</c>'s and tested there; these prove the crossing.
/// </summary>
public sealed class RuntimeDatabasesTest
{
    private static RuntimeRules Rules => RuntimeRulesTest.Rules;

    [Theory]
    [InlineData("postgresql")]
    [InlineData("mssql")]
    [InlineData("mariadb")]
    public void AnUpgradeToTheFirstReleaseIsNoneAndIsRefusedInWords(string server)
    {
        DatabaseUpgrade upgrade = Rules.Databases.Upgrade(server, 1, 0);

        Assert.False(upgrade.Ok);
        Assert.Empty(upgrade.Scripts);
        Assert.Contains("is none", upgrade.Refused, StringComparison.Ordinal);
        Assert.Equal(server, upgrade.Server);
    }

    [Fact]
    public void AServerXmipStorageKeepsNoDatabaseOnIsRefusedNamingTheOnesItDoes()
    {
        DatabaseUpgrade upgrade = Rules.Databases.Upgrade("rocksdb", 1, 2);

        Assert.False(upgrade.Ok);
        Assert.Contains("postgresql, mssql, mariadb", upgrade.Refused, StringComparison.Ordinal);
    }
}
