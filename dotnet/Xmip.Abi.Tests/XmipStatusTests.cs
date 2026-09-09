using Xmip.Abi.Module;

namespace Xmip.Abi.Tests;

/// <summary>
/// The binding agrees with section 3 of <c>xmip_module.h</c>. A status the
/// header has and the binding does not is a code that reaches an operator as
/// "not a status this build knows" — the exact failure ADR-0012 clause 1
/// says is a defect in the binding.
/// </summary>
public sealed class XmipStatusTests
{
    private const string Unknown = "not a status this build knows";

    [Fact]
    public void FindsStatusesInTheHeader()
    {
        Assert.NotEmpty(Header.Statuses());
    }

    [Fact]
    public void KnowsEveryStatusTheHeaderDefines()
    {
        foreach ((string constant, int code) in Header.Statuses())
        {
            Assert.True(Enum.IsDefined((XmipStatus)code), $"XMIP_{constant} is {code}");
        }
    }

    [Fact]
    public void GivesEachStatusTheNameTheHeaderImplies()
    {
        foreach ((string constant, int code) in Header.Statuses())
        {
            Assert.Equal(Header.MemberName(constant), ((XmipStatus)code).ToString());
        }
    }

    [Fact]
    public void DefinesNoStatusTheHeaderLacks()
    {
        // The other direction: a member here with no constant there is a
        // code no module can ever return, which is the binding inventing
        // boundary.
        IReadOnlyDictionary<string, int> header = Header.Statuses();

        foreach (XmipStatus status in Enum.GetValues<XmipStatus>())
        {
            Assert.Contains((int)status, header.Values);
        }
    }

    [Fact]
    public void RetriesExactlyWhatTheHeaderSaysIsRetryable()
    {
        // XMIP_IS_RETRYABLE in the header: timeout, unavailable, capacity,
        // again. Not Io, which covers faults that repeat. Getting this wrong
        // makes xmip-core-resilience retry something that cannot succeed, or
        // give up on something that would have.
        string[] retryable = ["E_TIMEOUT", "E_UNAVAILABLE", "E_CAPACITY", "E_AGAIN"];

        foreach ((string constant, int code) in Header.Statuses())
        {
            bool expected = retryable.Contains(constant, StringComparer.Ordinal);

            Assert.Equal(expected, ((XmipStatus)code).IsRetryable());
        }
    }

    [Fact]
    public void CallsTerminalExactlyWhatTheHeaderSaysIsTerminal()
    {
        string[] terminal = ["E_INTERNAL", "E_PANIC"];

        foreach ((string constant, int code) in Header.Statuses())
        {
            bool expected = terminal.Contains(constant, StringComparer.Ordinal);

            Assert.Equal(expected, ((XmipStatus)code).IsTerminal());
        }
    }

    [Fact]
    public void ExplainsEveryStatusItKnows()
    {
        foreach (XmipStatus status in Enum.GetValues<XmipStatus>())
        {
            Assert.NotEqual(Unknown, status.Explain());
        }
    }

    [Fact]
    public void SaysSoRatherThanThrowingOnACodeItDoesNotKnow()
    {
        // A number that is not a status is data, not an exception. A module
        // returning something unexpected must not take the operator's session
        // down with it.
        XmipStatus status = (XmipStatus)4711;

        Assert.Equal(Unknown, status.Explain());
        Assert.False(status.IsRetryable());
        Assert.False(status.IsTerminal());
    }
}
