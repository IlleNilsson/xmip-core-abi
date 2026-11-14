using Xmip.Abi.Module;

namespace Xmip.Surface.Test;

/// <summary>
/// An act at a node's door comes to one of three outcomes (ADR-0014,
/// amendment 2026-11-13): taken, asked — written to Xmip Storage and not
/// taken yet, the status <c>XMIP_E_AGAIN</c>, which is no failure — or
/// refused; every act's record carries it, and every command says it first
/// in words.
/// </summary>
public sealed class ActOutcomeTest
{
    [Fact]
    public void AStatusOfTheActIsItsOutcome()
    {
        Assert.Equal(ActOutcome.Taken, ActOutcomes.Of(XmipStatus.Ok));
        Assert.Equal(ActOutcome.Asked, ActOutcomes.Of(XmipStatus.Again));
        foreach (XmipStatus refused in new[]
                 {
                     XmipStatus.Auth, XmipStatus.NotFound, XmipStatus.Invalid,
                     XmipStatus.Io, XmipStatus.Unavailable,
                 })
        {
            Assert.Equal(ActOutcome.Refused, ActOutcomes.Of(refused));
        }

        Assert.Equal("asked", ActOutcome.Asked.Word());
    }

    [Fact]
    public void ARecordSaysTakenOrRefusedByWhetherItWasAppliedUnlessTheDoorSaidAsked()
    {
        string Node = TestCluster.Read().NodeScope(0);
        SubscriptionOperation paused =
            new(Node, "Orders", SubscriptionAct.Pause, true, "paused");
        Assert.Equal(ActOutcome.Taken, paused.Outcome);
        Assert.Equal(
            ActOutcome.Refused,
            new MessageOperation(Node, "j-1", MessageAct.Retry, false, "REFUSED: gone").Outcome);
        DeadMessageOperation asked = new(Node, "m-1", DeadMessageAct.Replay, false, "asked of it")
        {
            Outcome = ActOutcome.Asked,
        };
        Assert.False(asked.Applied, "asked is not taken");
        Assert.Equal(ActOutcome.Asked, asked.Outcome);
    }

    [Fact]
    public void ACommandSaysTheOutcomeFirstInWords()
    {
        Assert.Equal("OK. paused", ActOutcomes.Said(ActOutcome.Taken, "paused"));
        Assert.Equal(
            "ASKED. asked of receiving; not taken yet",
            ActOutcomes.Said(ActOutcome.Asked, "asked of receiving; not taken yet"));
        Assert.Equal("REFUSED: gone", ActOutcomes.Said(ActOutcome.Refused, "gone"));
    }
}
