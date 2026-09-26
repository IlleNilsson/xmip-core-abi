using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// A .NET face subscribes to Events through the runtime the one discovery
/// rule found, and its subscription is audited where the program's own
/// records go (ADR-0065, ADR-0062). The crossing is Xmip.Abi's and tested
/// there; these prove the face's API over it. Every audit goes to a
/// temporary directory, never the operating system's log.
/// </summary>
public sealed class EventFeedTest
{
    private const string Party = "0198a3c4-0000-7000-8000-000000000043";

    private static string Scratch()
    {
        return Path.Combine(Path.GetTempPath(), $"xmip-surface-event-{Guid.NewGuid():n}");
    }

    private static EventRecord Raised(string scope, EventOutcome outcome)
    {
        return new EventRecord
        {
            Type = "se.xmip.send.test",
            Action = EventAction.Send,
            Outcome = outcome,
            Scope = scope + "/node/n/send/partner-x",
        };
    }

    [Fact]
    public void ASubscriptionIsDrainedAndAuditedAsTheProgram()
    {
        string directory = Scratch();
        string scope = $"xmip:///surface-event-{Guid.NewGuid():n}";
        EventFeed feed = new(new ProgramAudit("Xmip.Surface.Test", directory), Party);

        using (EventSubscription subscription = feed.Subscribe(new EventFilter { Scope = scope }))
        {
            Assert.Equal(1, EventFeed.Publish(Raised(scope, EventOutcome.Rejection)));

            EventRecord heard = Assert.Single(
                subscription.Next(TimeSpan.FromSeconds(2), 16).Events);

            Assert.Equal(EventOutcome.Rejection, heard.Outcome);
            Assert.Equal(scope + "/node/n/send/partner-x", heard.Scope);
        }

        // Unsubscribing waits until every record is kept.
        string text = File.ReadAllText(Path.Combine(directory, "audit.toml"));
        Assert.Contains("program = \"Xmip.Surface.Test\"", text, StringComparison.Ordinal);
        Assert.Contains("action = \"event.subscribe\"", text, StringComparison.Ordinal);
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task FollowingYieldsEachEventAsItArrivesAndStopsWhenCancelled()
    {
        string directory = Scratch();
        string scope = $"xmip:///surface-follow-{Guid.NewGuid():n}";
        EventFeed feed = new(new ProgramAudit("Xmip.Surface.Test", directory), Party);
        using CancellationTokenSource stop = new();
        List<EventRecord> heard = [];

        Task following = Task.Run(async () =>
        {
            await foreach (EventRecord raised in feed.Follow(
                new EventFilter { Scope = scope }, stop.Token))
            {
                heard.Add(raised);

                if (heard.Count == 2)
                {
                    await stop.CancelAsync();
                }
            }
        });

        // The subscription opens when the enumeration starts; until then a
        // publish reaches nobody.
        for (int i = 0; i < 2000 && EventFeed.Publish(Raised(scope, EventOutcome.Success)) == 0;
            i++)
        {
            await Task.Delay(1);
        }

        Assert.Equal(1, EventFeed.Publish(Raised(scope, EventOutcome.Failure)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => following.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal([EventOutcome.Success, EventOutcome.Failure], heard.Select(e => e.Outcome));
        Assert.Equal(0, EventFeed.Publish(Raised(scope, EventOutcome.Success)));
        Directory.Delete(directory, recursive: true);
    }
}
