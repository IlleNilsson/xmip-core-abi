using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// A .NET face subscribes to Events through the runtime the one discovery
/// rule found, as the Party the account it runs as proves to be among the
/// Parties of the node serving in its process, and names none (ADR-0065;
/// ADR-0019, amendment 2026-11-11). No node serves in a test's process, so
/// these prove the face refused, the refusal audited where the program's own
/// records go (ADR-0062), and a publish crossing. The crossing is
/// Xmip.Abi's and tested there; draining and following are proven where a
/// node serves, in the runtime's own tests. Every audit goes to a temporary
/// directory, never the operating system's log.
/// </summary>
public sealed class EventFeedTest
{
    private const string NoParty = "is no Party of a node serving in this process";

    private static readonly TestCluster Cluster = TestCluster.Read();

    // Where a raised Event sits beneath its scope: the sending node's send stage.
    private static readonly string Sent = $"/node/{Cluster.WithRole("sending")}/send/party-x";

    static EventFeedTest()
    {
        // Whatever the policy allows, a subscriber must prove to be a Party.
        RuntimeLibrary.Rules.Events.AuthorizeBy(_ => true);
    }

    private static string Scratch()
    {
        return Path.Combine(Path.GetTempPath(), $"xmip-surface-event-{Guid.NewGuid():n}");
    }

    [Fact]
    public void AFeedNamesNoPartyAndIsRefusedAndAuditedWhereNoNodeServes()
    {
        string directory = Scratch();
        string scope = $"{Cluster.Scope}/surface-event-{Guid.NewGuid():n}";
        EventFeed feed = new(new ProgramAudit("Xmip.Surface.Test", directory));

        UnauthorizedAccessException refused = Assert.Throws<UnauthorizedAccessException>(
            () => feed.Subscribe(new EventFilter { Scope = scope }));
        Assert.Contains(NoParty, refused.Message, StringComparison.Ordinal);

        string text = File.ReadAllText(Path.Combine(directory, "audit.toml"));
        Assert.Contains("program = \"Xmip.Surface.Test\"", text, StringComparison.Ordinal);
        Assert.Contains("action = \"event.subscribe\"", text, StringComparison.Ordinal);
        Assert.Contains(NoParty, text, StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task FollowingIsRefusedAsItStartsAndAPublishReachesNobody()
    {
        string directory = Scratch();
        string scope = $"{Cluster.Scope}/surface-follow-{Guid.NewGuid():n}";
        EventFeed feed = new(new ProgramAudit("Xmip.Surface.Test", directory));
        CancellationToken cancelled = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await foreach (EventRecord raised in feed.Follow(
                new EventFilter { Scope = scope }, cancelled))
            {
                Assert.Fail($"nothing is heard here, and {raised.Type} was");
            }
        });

        Assert.Equal(0, EventFeed.Publish(new EventRecord
        {
            Type = "se.xmip.send.test",
            Action = EventAction.Send,
            Outcome = EventOutcome.Success,
            Scope = scope + Sent,
        }));
        Directory.Delete(directory, recursive: true);
    }
}
