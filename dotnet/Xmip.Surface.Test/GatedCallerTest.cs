using System.Collections.Concurrent;
using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// Whom an act is for crosses as a parameter, never as ambient state
/// (ADR-0009, the second amendment of 2026-11-01): two callers acting at
/// once through the gate, each hopping threads between acts, each reach the
/// surface behind it as their own caller and no other, whatever caller an
/// act names.
/// </summary>
public sealed class GatedCallerTest : IDisposable
{
    private readonly string scratch =
        Path.Combine(Path.GetTempPath(), $"xmip-gated-{Guid.NewGuid():n}");

    public GatedCallerTest()
    {
        Directory.CreateDirectory(scratch);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(scratch, recursive: true);
        }
        catch (IOException)
        {
            // A file the platform still holds; the temp directory keeps it.
        }
    }

    [Fact]
    public async Task TwoCallersActingAtOnceEachReachTheSurfaceBehindTheGateAsTheirOwn()
    {
        Recording inner = new();
        ProgramAudit audit = new("Xmip.Surface.Test", scratch);
        using RoleContext first = new(Role.Operator, RoleAssignment.ProcessCaller());
        using RoleContext second = new(Role.Operator, RoleAssignment.ProcessCaller());
        ulong firstHandle = first.Caller?.Handle ?? 0;
        ulong secondHandle = second.Caller?.Handle ?? 0;
        Assert.NotEqual(firstHandle, secondHandle);

        CancellationToken cancelled = TestContext.Current.CancellationToken;
        await Task.WhenAll(
            Task.Run(() => Acting(inner, first, audit, "first", second.Caller), cancelled),
            Task.Run(() => Acting(inner, second, audit, "second", first.Caller), cancelled));

        Assert.Equal(200, inner.Acts.Count);
        Assert.All(inner.Acts, act => Assert.Equal(
            act.Journey == "first" ? firstHandle : secondHandle, act.Caller));
    }

    // A hundred acts through the gate, each after a hop to another thread,
    // each naming the other caller, which the gate never reads.
    private static async Task Acting(
        Recording inner, RoleContext context, ProgramAudit audit, string journey,
        HeldCaller? named)
    {
        GatedOperator gated = new(inner, context, audit);

        for (int each = 0; each < 100; each++)
        {
            await Task.Yield();
            MessageOperation done = gated.Act("scope", journey, MessageAct.Retry, "claimed", named);
            Assert.True(done.Applied, done.Result);
        }
    }

    // A surface that takes every act and keeps whom each was for.
    private sealed class Recording : IOperatorSurface
    {
        public ConcurrentBag<(string Journey, ulong Caller)> Acts { get; } = [];

        public string Source => "recording";

        public IReadOnlyList<HealthRecord> Health(string scope)
        {
            return [];
        }

        public MeasurementRecord? Measure(string scope, Counted counted)
        {
            return null;
        }

        public MessageOperation Act(
            string scope, string journey, MessageAct act, string who, HeldCaller? caller)
        {
            Acts.Add((journey, caller?.Handle ?? 0));
            return new MessageOperation(scope, journey, act, true, "taken");
        }

        public string PauseScope(string scope, string who, HeldCaller? caller)
        {
            return string.Empty;
        }

        public string ResumeScope(string scope, HeldCaller? caller)
        {
            return string.Empty;
        }
    }
}
