using System.Diagnostics;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// The surface over a node on this machine (ADR-0014, amendment
/// 2026-10-13), proved against a real local endpoint — a named pipe on
/// Windows, a Unix domain socket elsewhere — served by the runtime this
/// estate built, over gRPC with no certificate: it answers what the node
/// publishes, it is told when that advances and how fast, measured end to
/// end into .NET against the millisecond rule; an act is the user this
/// process runs as and is admitted by the role the node grants; a node lost
/// is said so; and a surface that names no node says that. Run alone
/// (<see cref="Measured"/>), so the time is Xmip's and not the run's.
/// </summary>
/// <remarks>
/// The notice is held by what is Xmip's alone, as the Event's is
/// (<c>EventLatencyTest</c>), each figure as the median of the fastest round
/// in every five: a poll or a busy loop is in every round and is in the
/// fastest of five too, where a wake that waited its turn for a processor,
/// or a thread of the .NET runtime compiling beside it, is in some rounds
/// only. <b>The wake is a wake</b>: from post to notice is under a
/// millisecond plus a plain wake across a pipe, measured beside it round by
/// round — a thread woken on a kernel event writes a byte and an awaiting
/// read continues, the notice's kind of hop without Xmip's work in it. A
/// poll, a missed notify or a timeout waited out puts every round at its
/// interval and fails it. <b>Xmip's own work is under a millisecond</b>: the
/// processor time this process spends from the post to the notice in hand,
/// every thread of it — the runtime's notify and write, the surface's read,
/// decode and continuation — counted by <see cref="ProcessorTime"/>, which
/// waiting cannot reach, is under a millisecond plus what the process spends
/// across the plain wake. That one is not nothing: on 2026-10-17 a plain
/// wake of 0.14 milliseconds cost the process about 0.3 of processor time,
/// the .NET runtime's own threads spinning beside it, and a notice about 0.8.
/// The median and the tail of all the rounds are printed and never
/// asserted: each of the notice's hops is a wake, and on a machine busy on
/// every processor each waits its turn, so a median held alone ran from 0.1
/// to 30 milliseconds and measured the scheduler; under sixteen busy
/// processes, on 2026-10-17, it ran from 0.8 to 2.4 while the fastest of five
/// stayed between 0.5 and 0.7.
/// </remarks>
[Collection(Measured.Name)]
public sealed class LocalOperatorTest
{
    private const int Warm = 20;
    private const int Rounds = 200;
    private const double Bound = 1.0;
    private const int Five = 5;

    private static readonly TestCluster Cluster = TestCluster.Read();

    [Fact]
    public async Task AnswersWhatTheNodePublishesAndIsToldWithinAMillisecond()
    {
        string published = Fixture("snapshot.toml");
        using ServedNode served = Serve(published);
        using LocalOperator localNode = new(served.Address);
        IOperatorSurface surface = localNode;
        SnapshotOperator snapshot = new(FixturePath("snapshot.toml"));

        Assert.True(localNode.Connect(), localNode.Reason);
        Assert.Equal($"LOCAL — {served.Address}", surface.Source);
        Assert.Equal(snapshot.Health(ScopeTree.Root), surface.Health(ScopeTree.Root));

        using CancellationTokenSource patience = new(TimeSpan.FromSeconds(30));
        await using IAsyncEnumerator<SurfaceChange> feed =
            surface.WatchAsync(patience.Token).GetAsyncEnumerator(patience.Token);

        Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
        Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
        ulong first = feed.Current.Revision;

        // The millisecond rule: from the node posting its next publication to
        // the notice in a .NET watch's hand, over many rounds, each beside a
        // plain wake across a pipe. The notice comes first; the publication
        // is read by the question after it.
        List<double> took = [];
        List<double> worked = [];
        List<double> plain = [];
        List<double> plainWorked = [];
        using PlainWake control = new();

        for (int round = 1; round <= Warm + Rounds; round++)
        {
            long posted = Stopwatch.GetTimestamp();
            long from = ProcessorTime.Process();
            served.Post(published + $"\n# round {round}\n");
            Assert.True(await feed.MoveNextAsync().ConfigureAwait(true));
            worked.Add(ProcessorTime.Milliseconds(ProcessorTime.Process() - from));
            took.Add(Stopwatch.GetElapsedTime(posted).TotalMilliseconds);
            Assert.Equal(first + (ulong)round, feed.Current.Revision);

            (double wall, double work) = await control.Round().ConfigureAwait(true);
            plain.Add(wall);
            plainWorked.Add(work);
        }

        double median = Spread("a local follow told into .NET", took);
        double usual = Spread("a plain wake across a pipe beside it", plain);
        double own = Spread("the process's processor time for a notice", worked);
        double idle = Spread("the process's processor time for a plain wake", plainWorked);
        Assert.True(
            median < Bound + usual,
            $"the fastest of five, median {median:F3} ms, the machine's own {usual:F3} ms");
        Assert.True(
            own < Bound + idle,
            $"the process's processor time for a notice, the fastest of five, median "
            + $"{own:F3} ms, for a plain wake {idle:F3} ms");
        Assert.Equal(snapshot.Health(ScopeTree.Root), surface.Health(ScopeTree.Root));
    }

    [Fact]
    public void AnActIsTheUsersAndIsAdmittedByTheNodesRole()
    {
        string node = $"{Cluster.Scope}/node/{Cluster.WithRole("sending")}";

        using (ServedNode observed = Serve(Fixture("snapshot.toml")))
        using (LocalOperator local = new(observed.Address))
        {
            MessageOperation refused = local.Act(node, "j1", MessageAct.Retry, "a name");
            Assert.False(refused.Applied);
            Assert.StartsWith("REFUSED. ", refused.Result, StringComparison.Ordinal);
            Assert.Contains(
                Environment.UserName, refused.Result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("a name", refused.Result, StringComparison.Ordinal);
        }

        using ServedNode operated = Serve(Fixture("snapshot.toml"), "operator");
        using LocalOperator acting = new(operated.Address);
        MessageOperation taken = acting.Act(node, "j1", MessageAct.Retry, "a name");
        Assert.False(taken.Applied);
        Assert.Equal($"REFUSED: no node at {node} runs in this process", taken.Result);
    }

    [Fact]
    public async Task ANodeLostIsToldAndItsLastPublicationIsNotAnsweredAsCurrent()
    {
        ServedNode served = Serve(Fixture("snapshot.toml"));
        using LocalOperator local = new(served.Address);
        IOperatorSurface surface = local;
        using CancellationTokenSource patience = new(TimeSpan.FromSeconds(30));
        await using IAsyncEnumerator<SurfaceChange> feed =
            surface.WatchAsync(patience.Token).GetAsyncEnumerator(patience.Token);

        Assert.Equal($"LOCAL — {served.Address}", await Next(feed, Reached).ConfigureAwait(true));
        Assert.NotEmpty(surface.Health(ScopeTree.Root));

        Stopwatch stopped = Stopwatch.StartNew();
        served.Dispose();
        Assert.True(stopped.Elapsed < TimeSpan.FromSeconds(1), $"{stopped.Elapsed}");

        string lost = await Next(feed, source => !Reached(source)).ConfigureAwait(true);
        Assert.Contains(" unreachable: ", lost, StringComparison.Ordinal);
        Assert.Empty(surface.Health(ScopeTree.Root));
    }

    [Fact]
    public void ANodeThatIsNotThereAndNoNodeNamedAreSaidSo()
    {
        using LocalOperator absent = new($"xmip-nobody-{Guid.NewGuid():n}");
        Assert.False(absent.Connect());
        Assert.Contains(" unreachable: ", absent.Source, StringComparison.Ordinal);
        Assert.Empty(absent.Health(ScopeTree.Root));

        using LocalOperator unnamed = new(null);
        Assert.False(unnamed.Connect());
        Assert.Contains(LocalOperator.Unnamed, unnamed.Source, StringComparison.Ordinal);
        Assert.NotNull(LocalOperator.Refusal(" "));
        Assert.Null(LocalOperator.Refusal("xmip-node"));
    }

    /// <summary>Decided by the owner, 2026-10-13: <i>a local call waits at most
    /// a set time — <c>[tuning]</c>, default 2 s — then says the node did not
    /// answer and shows it so; the tool recovers on its own.</i> An endpoint
    /// that takes the connection and never answers stands for a hung
    /// node.</summary>
    [Fact]
    public void ANodeThatDoesNotAnswerWithinTheWaitIsShownUnreachable()
    {
        string name = $"xmip-silent-test-{Guid.NewGuid():n}";
        using IDisposable silent = Silent(name);
        using LocalOperator local = new(name, TimeSpan.FromMilliseconds(200));

        Stopwatch asked = Stopwatch.StartNew();
        Assert.False(local.Connect());
        Assert.True(asked.Elapsed < TimeSpan.FromSeconds(2), $"{asked.Elapsed}");
        Assert.Contains(
            "the node did not answer within 200 ms", local.Source, StringComparison.Ordinal);
        Assert.Empty(local.Health(ScopeTree.Root));
    }

    // An endpoint called name that accepts a connection and says nothing.
    private static IDisposable Silent(string name)
    {
        if (OperatingSystem.IsWindows())
        {
            System.IO.Pipes.NamedPipeServerStream pipe = new(
                name, System.IO.Pipes.PipeDirection.InOut, 1,
                System.IO.Pipes.PipeTransmissionMode.Byte,
                System.IO.Pipes.PipeOptions.Asynchronous);
            _ = pipe.WaitForConnectionAsync();
            return pipe;
        }

        string folder = OperatingSystem.IsLinux() ? "/run/xmip/" : "/var/run/xmip/";
        System.Net.Sockets.Socket socket = new(
            System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream,
            System.Net.Sockets.ProtocolType.Unspecified);
        socket.Bind(new System.Net.Sockets.UnixDomainSocketEndPoint($"{folder}{name}.sock"));
        socket.Listen(1);
        return socket;
    }

    private static string FixturePath(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixture", name);
    }

    private static string Fixture(string name)
    {
        return File.ReadAllText(FixturePath(name));
    }

    /// <summary>A local endpoint no other test uses, served by the runtime
    /// this estate built, answering <paramref name="publication"/>, every
    /// caller holding <paramref name="role"/>.</summary>
    private static ServedNode Serve(string publication, string role = "")
    {
        ServedNode served = RuntimeLibrary.Rules.Remotes.ServeLocal(
            $"xmip-local-operator-test-{Guid.NewGuid():n}", role, "local-operator-test",
            out string reason)
            ?? throw new InvalidOperationException(reason);
        served.Post(publication);
        return served;
    }

    // The median of the fastest round in each five after the warm-up, what
    // is held; printed with the median, the 99th percentile and the worst of
    // all of them, which are not.
    private static double Spread(string what, List<double> took)
    {
        double[] sorted = [.. took.Skip(Warm).Order()];
        double[] fastest = [.. took.Skip(Warm).Chunk(Five).Select(five => five.Min()).Order()];
        double held = fastest[fastest.Length / 2];

        Console.WriteLine(
            $"{what}: {sorted.Length} rounds, the fastest of five {held:F3} ms; "
            + $"median {sorted[sorted.Length / 2]:F3} ms, "
            + $"p99 {sorted[(int)Math.Ceiling(sorted.Length * 0.99) - 1]:F3} ms, "
            + $"worst {sorted[^1]:F3} ms");

        return held;
    }

    /// <summary>The machine's own wake across a pipe, with nothing of Xmip
    /// in it: a thread blocked on a kernel event is woken, writes one byte
    /// to a named pipe (a Unix domain socket elsewhere), and the awaiting
    /// read continues where the test awaits, as a notice's does.</summary>
    private sealed class PlainWake : IDisposable
    {
        private readonly System.IO.Pipes.NamedPipeServerStream reader;
        private readonly System.IO.Pipes.NamedPipeClientStream writer;
        private readonly AutoResetEvent told = new(false);
        private readonly Thread waker;
        private readonly byte[] heard = new byte[1];
        private bool done;

        public PlainWake()
        {
            string name = $"xmip-plain-wake-test-{Guid.NewGuid():n}";
            reader = new(
                name, System.IO.Pipes.PipeDirection.In, 1,
                System.IO.Pipes.PipeTransmissionMode.Byte,
                System.IO.Pipes.PipeOptions.Asynchronous);
            writer = new(
                ".", name, System.IO.Pipes.PipeDirection.Out,
                System.IO.Pipes.PipeOptions.Asynchronous);
            Task connected = reader.WaitForConnectionAsync();
            writer.Connect(5_000);
            connected.GetAwaiter().GetResult();

            waker = new(() =>
            {
                while (told.WaitOne() && !Volatile.Read(ref done))
                {
                    writer.WriteByte(1);
                    writer.Flush();
                }
            })
            { IsBackground = true, Name = "plain wake" };
            waker.Start();
        }

        /// <summary>One wake: the milliseconds from the event set to the
        /// read continued, and the process's processor time over them. The
        /// read is waiting before the clocks start, as the watch's is.</summary>
        public async Task<(double Wall, double Work)> Round()
        {
            ValueTask<int> read = reader.ReadAsync(heard);
            long woken = Stopwatch.GetTimestamp();
            long from = ProcessorTime.Process();
            told.Set();
            Assert.Equal(1, await read.ConfigureAwait(true));
            return (
                Stopwatch.GetElapsedTime(woken).TotalMilliseconds,
                ProcessorTime.Milliseconds(ProcessorTime.Process() - from));
        }

        public void Dispose()
        {
            Volatile.Write(ref done, true);
            told.Set();
            waker.Join();
            writer.Dispose();
            reader.Dispose();
            told.Dispose();
        }
    }

    private static bool Reached(string source)
    {
        return !source.Contains(" unreachable: ", StringComparison.Ordinal);
    }

    private static async Task<string> Next(
        IAsyncEnumerator<SurfaceChange> feed, Func<string, bool> wanted)
    {
        while (await feed.MoveNextAsync().ConfigureAwait(true))
        {
            if (wanted(feed.Current.Source))
            {
                return feed.Current.Source;
            }
        }

        throw new InvalidOperationException("the feed ended");
    }
}
