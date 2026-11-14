using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// <see cref="RuntimeMessages"/> crosses section 16 to the runtime this
/// estate built and back (runtime-model.md section 13; ADR-0013): no node
/// runs in a test's process, so no Send Port is listed with a Message that
/// failed. What a Retry sends again is the runtime's and tested there, and
/// Retry and Dismiss are asked at a node's door (ADR-0014, amendment
/// 2026-11-13); this proves the crossing.
/// </summary>
public sealed class RuntimeMessagesTest
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    // The test cluster's sending node, by what it declares.
    private static readonly string Node = $"{Cluster.Scope}/node/{Cluster.WithRole("sending")}";

    private static RuntimeRules Rules => RuntimeRulesTest.Rules;

    [Fact]
    public void NoNodeRunsHereSoNoSendPortIsListed()
    {
        FailedMessageList failed = Rules.Messages.Failed(Node, string.Empty, 0, 0);

        Assert.True(failed.Listed);
        Assert.Empty(failed.Ports);
    }
}
