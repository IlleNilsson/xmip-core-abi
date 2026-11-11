using Xmip.Abi.Operate;

namespace Xmip.Surface.Test;

/// <summary>
/// <see cref="ReportCatalog"/> reads every Report and one opened from the
/// runtime this estate built (ADR-0072, amendment 2026-11-10): the kinds and
/// their words, each entry's kind, availability and last probe, a probe's
/// tally and rows and one requirement in full — every word the runtime's,
/// none listed here. Which Reports there are is <c>xmip-core-report</c>'s
/// and tested there; an entry is found here by what it holds, never by its
/// name.
/// </summary>
public sealed class ReportCatalogTest : IDisposable
{
    private static readonly TestCluster Cluster = TestCluster.Read();

    private readonly string _directory =
        Directory.CreateTempSubdirectory("xmip-surface-report-").FullName;

    private ProgramAudit Audit => new("Xmip.Surface.Test", _directory);

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void EveryEntrySaysItsKindInTheRuntimesWords()
    {
        ReportListing listing = ReportCatalog.List(Audit);

        Assert.Equal(Enum.GetValues<ReportKind>(), listing.Kinds.Select(words => words.Kind));
        Assert.All(listing.Kinds, words =>
        {
            Assert.NotEmpty(words.Word);
            Assert.NotEmpty(words.Name);
            Assert.NotEmpty(words.Meaning);
        });
        Assert.All(listing.Entries, entry => Assert.Same(
            listing.Kinds.Single(words => words.Kind == entry.Kind), entry.Words));
        Assert.Null(listing.Unread);

        // A quick entry available is presented by a reader of the header; one
        // not yet available says so and has nothing to open.
        Assert.Contains(listing.Entries, entry => entry is
        {
            Kind: ReportKind.ExistingData, Available: true, ReadBy: not null, Expect: null,
        });
        Assert.Contains(listing.Entries, entry => entry is
        {
            Available: false, ReadBy: null, Parameter: null, Expect: not null,
        });
        ReportEntry quest = Probing(listing);
        Assert.NotEmpty(quest.Levels);
        Assert.NotNull(quest.Row);
        Assert.Null(quest.TookMilliseconds);
        Assert.NotNull(quest.Expect);
    }

    [Fact]
    public void AProbeIsReadAtEachLevelAndListedAsTheLastProbe()
    {
        ReportEntry quest = Probing(ReportCatalog.List(Audit));
        ReportProbe probe = new(quest.Name, Cluster.Path)
        {
            Parameter = quest.Parameter?.Choices[0],
            Node = Cluster.WithRole("storage"),
        };

        ReportProbed probed = ReportCatalog.Open(probe, Audit);

        Assert.Equal(quest.Name, probed.Report);
        Assert.Equal(Cluster.WithRole("storage"), probed.Node);
        Assert.NotEmpty(probed.Title);
        Assert.NotEmpty(probed.Rows);
        Assert.Equal(quest.Row, probed.Row);
        Assert.NotEmpty(probed.Tally);
        Assert.Equal(probed.Rows.Count, probed.Tally.Sum(count => count.Count));
        Assert.All(
            probed.Rows,
            row => Assert.Contains(probed.Tally, count => count.State == row.State));
        Assert.Null(probed.Requirement);

        ReportRow row = probed.Rows[0];
        ReportRequirement full = ReportCatalog.Open(probe with { Row = row.Name }, Audit)
            .Requirement ?? throw new InvalidOperationException("no requirement");
        Assert.Equal((row.Name, row.Citation, row.State), (full.Name, full.Citation, full.State));
        Assert.NotEmpty(full.Says);

        ReportEntry probedLast = Probing(ReportCatalog.List(Audit));
        Assert.NotNull(probedLast.TookMilliseconds);
        Assert.NotNull(probedLast.ProbedAt);
    }

    [Fact]
    public void ARefusalIsTheRuntimesSentenceAndAnUnknownNameNamesThoseThereAre()
    {
        ReportListing listing = ReportCatalog.List(Audit);
        ReportEntry quick = listing.Entries.First(entry => entry.ReadBy is not null);

        // A cluster's file and no node: the Report is refused for what it is,
        // before the file is read.
        ReportProbe probe = new(quick.Name, Cluster.Path);

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => ReportCatalog.Open(probe, Audit));
        Assert.StartsWith("REFUSED:", refused.Message, StringComparison.Ordinal);
        Assert.Contains(quick.ReadBy!, refused.Message, StringComparison.Ordinal);

        InvalidOperationException unknown = Assert.Throws<InvalidOperationException>(
            () => ReportCatalog.Named(Audit, "nowhere"));
        Assert.StartsWith("REFUSED:", unknown.Message, StringComparison.Ordinal);
        Assert.Contains(quick.Name, unknown.Message, StringComparison.Ordinal);

        ReportListing one = ReportCatalog.Named(Audit, quick.Name);
        Assert.Equal(listing.Kinds, one.Kinds);
        ReportEntry named = Assert.Single(one.Entries);
        Assert.Equal(
            (quick.Name, quick.Kind, quick.ReadBy), (named.Name, named.Kind, named.ReadBy));
    }

    [Fact]
    public void AnAnswerWithoutATallyOrATitleReadsAsNoneAndNeverAsZeros()
    {
        ReportProbed probed = ReportCatalog.Probed(
            """{"report":"A module's","node":"n","row":"finding","took_ms":1,"found":true}""");

        Assert.Empty(probed.Tally);
        Assert.Empty(probed.Rows);
        Assert.Empty(probed.Title);
        Assert.Equal("finding", probed.Row);
        Assert.Null(probed.Requirement);
    }

    [Fact]
    public void ARequirementsBehindAndActionsAreReadMemberByMember()
    {
        ReportProbed probed = ReportCatalog.Probed(
            """
            {"report":"Q","node":"n","row":"requirement","took_ms":3,"title":"T",
             "tally":[{"state":"evidence","count":0},{"state":"gap","count":1}],
             "rows":[{"name":"r","citation":"c","state":"gap"}],
             "requirement":{"name":"r","citation":"c","state":"gap","says":"s",
              "behind":[{"kind":"location","side":"receive","name":"in"}],
              "actions":[{"kind":"configure","setting":"k","says":"set k"},
                         {"kind":"outside","says":"o"}]}}
            """);

        ReportRequirement full = probed.Requirement!;
        Assert.Equal(3, probed.TookMilliseconds);
        Assert.Null(probed.Parameter);
        Assert.Equal(["evidence 0", "gap 1"], probed.Tally.Select(count => $"{count}"));
        Assert.Equal("location: receive in", full.Behind[0].ToString());
        Assert.Equal(["configure k: set k", "outside: o"], full.Actions.Select(a => $"{a}"));
    }

    // The first Quest's entry that is built: the one a probe opens.
    private static ReportEntry Probing(ReportListing listing)
    {
        return listing.Entries.First(entry => entry is
        {
            Kind: ReportKind.Quest, Available: true,
        });
    }
}
