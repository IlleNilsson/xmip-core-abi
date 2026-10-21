using Xmip.Abi.Operate;

namespace Xmip.Abi.Test;

/// <summary>
/// The binding agrees with <c>xmip_operate.h</c>: its version, its
/// symbols, and every health mood and counted kind. ADR-0027 clause 2 makes
/// the version its own; the first test also proves it has not quietly become
/// the module boundary's.
/// </summary>
public sealed class OperateAbiTest
{
    [Fact]
    public void SpeaksTheOperateVersionTheHeaderDeclares()
    {
        Assert.Equal(
            Header.Define("xmip_operate.h", "XMIP_OPERATE_VERSION"),
            $"{OperateAbi.Version}");
    }

    [Theory]
    [InlineData("XMIP_OPERATE_ENTRYPOINT", OperateAbi.Entrypoint)]
    [InlineData("XMIP_START_ENTRYPOINT", OperateAbi.StartEntrypoint)]
    [InlineData("XMIP_VALIDATE_ENTRYPOINT", OperateAbi.ValidateEntrypoint)]
    [InlineData("XMIP_SCOPE_CONTAINS_ENTRYPOINT", OperateAbi.ScopeContainsEntrypoint)]
    [InlineData("XMIP_SCOPE_MATCHES_ENTRYPOINT", OperateAbi.ScopeMatchesEntrypoint)]
    [InlineData("XMIP_RUN_SHOWN_ENTRYPOINT", OperateAbi.RunShownEntrypoint)]
    [InlineData("XMIP_SCOPE_PARTS_ENTRYPOINT", OperateAbi.ScopePartsEntrypoint)]
    [InlineData("XMIP_SCOPE_NODE_ENTRYPOINT", OperateAbi.ScopeNodeEntrypoint)]
    [InlineData("XMIP_STAGE_WORDS_ENTRYPOINT", OperateAbi.StageWordsEntrypoint)]
    [InlineData("XMIP_ROLE_WORDS_ENTRYPOINT", OperateAbi.RoleWordsEntrypoint)]
    [InlineData("XMIP_ROLE_DECLARED_ENTRYPOINT", OperateAbi.RoleDeclaredEntrypoint)]
    [InlineData("XMIP_ROLE_STAGES_ENTRYPOINT", OperateAbi.RoleStagesEntrypoint)]
    [InlineData("XMIP_HEALTH_WORD_ENTRYPOINT", OperateAbi.HealthWordEntrypoint)]
    [InlineData("XMIP_HEALTH_COLOR_ENTRYPOINT", OperateAbi.HealthColorEntrypoint)]
    [InlineData("XMIP_HEALTH_NAMED_ENTRYPOINT", OperateAbi.HealthNamedEntrypoint)]
    [InlineData("XMIP_HEALTH_ORDER_ENTRYPOINT", OperateAbi.HealthOrderEntrypoint)]
    [InlineData("XMIP_HEALTH_ROLLED_ENTRYPOINT", OperateAbi.HealthRolledEntrypoint)]
    [InlineData("XMIP_COUNTED_WORD_ENTRYPOINT", OperateAbi.CountedWordEntrypoint)]
    [InlineData("XMIP_STAGE_COUNTED_ENTRYPOINT", OperateAbi.StageCountedEntrypoint)]
    [InlineData("XMIP_STAGE_PAUSABLE_ENTRYPOINT", OperateAbi.StagePausableEntrypoint)]
    [InlineData("XMIP_STAGE_LOCATION_ENTRYPOINT", OperateAbi.StageLocationEntrypoint)]
    [InlineData(
        "XMIP_CAPABILITY_PUBLISHED_ENTRYPOINT", OperateAbi.CapabilityPublishedEntrypoint)]
    [InlineData("XMIP_CAPABILITY_ENTRY_ENTRYPOINT", OperateAbi.CapabilityEntryEntrypoint)]
    [InlineData("XMIP_PUBLICATION_READ_ENTRYPOINT", OperateAbi.PublicationReadEntrypoint)]
    [InlineData("XMIP_PUBLICATION_FREE_ENTRYPOINT", OperateAbi.PublicationFreeEntrypoint)]
    [InlineData("XMIP_PUBLICATION_HEAD_ENTRYPOINT", OperateAbi.PublicationHeadEntrypoint)]
    [InlineData("XMIP_PUBLICATION_RECORDS_ENTRYPOINT", OperateAbi.PublicationRecordsEntrypoint)]
    [InlineData("XMIP_PUBLICATION_COUNTS_ENTRYPOINT", OperateAbi.PublicationCountsEntrypoint)]
    [InlineData("XMIP_PUBLICATION_NODES_ENTRYPOINT", OperateAbi.PublicationNodesEntrypoint)]
    [InlineData("XMIP_PUBLICATION_LINKS_ENTRYPOINT", OperateAbi.PublicationLinksEntrypoint)]
    [InlineData("XMIP_PUBLICATION_RUN_ENTRYPOINT", OperateAbi.PublicationRunEntrypoint)]
    [InlineData("XMIP_TOPOLOGY_KIND_WORDS_ENTRYPOINT", OperateAbi.TopologyKindWordsEntrypoint)]
    [InlineData(
        "XMIP_TOPOLOGY_ORIGIN_WORDS_ENTRYPOINT", OperateAbi.TopologyOriginWordsEntrypoint)]
    [InlineData(
        "XMIP_TOPOLOGY_PATTERN_WORDS_ENTRYPOINT", OperateAbi.TopologyPatternWordsEntrypoint)]
    [InlineData("XMIP_CURVE_READ_ENTRYPOINT", OperateAbi.CurveReadEntrypoint)]
    [InlineData("XMIP_CURVE_POINTS_ENTRYPOINT", OperateAbi.CurvePointsEntrypoint)]
    [InlineData("XMIP_CURVE_FREE_ENTRYPOINT", OperateAbi.CurveFreeEntrypoint)]
    [InlineData("XMIP_AUDIT_ENTRYPOINT", OperateAbi.AuditEntrypoint)]
    [InlineData("XMIP_AUDIT_READ_ENTRYPOINT", OperateAbi.AuditReadEntrypoint)]
    [InlineData("XMIP_EVENT_SOURCE", OperateAbi.EventSource)]
    [InlineData("XMIP_EVENT_SOURCE_UNREGISTERED", OperateAbi.EventSourceUnregistered)]
    [InlineData("XMIP_CLUSTER_VIEWS_ENTRYPOINT", OperateAbi.ClusterViewsEntrypoint)]
    [InlineData("XMIP_FILTER_STRUCTURE_ENTRYPOINT", OperateAbi.FilterStructureEntrypoint)]
    [InlineData("XMIP_FILTER_TEXT_ENTRYPOINT", OperateAbi.FilterTextEntrypoint)]
    [InlineData("XMIP_CLUSTER_EDIT_ENTRYPOINT", OperateAbi.ClusterEditEntrypoint)]
    [InlineData("XMIP_CLUSTER_SLICES_ENTRYPOINT", OperateAbi.ClusterSlicesEntrypoint)]
    [InlineData("XMIP_EVENT_SUBSCRIBE_ENTRYPOINT", OperateAbi.EventSubscribeEntrypoint)]
    [InlineData("XMIP_EVENT_NEXT_ENTRYPOINT", OperateAbi.EventNextEntrypoint)]
    [InlineData("XMIP_EVENT_BATCH_FREE_ENTRYPOINT", OperateAbi.EventBatchFreeEntrypoint)]
    [InlineData("XMIP_EVENT_LISTEN_ENTRYPOINT", OperateAbi.EventListenEntrypoint)]
    [InlineData("XMIP_EVENT_UNSUBSCRIBE_ENTRYPOINT", OperateAbi.EventUnsubscribeEntrypoint)]
    [InlineData("XMIP_EVENT_PUBLISH_ENTRYPOINT", OperateAbi.EventPublishEntrypoint)]
    [InlineData("XMIP_EVENT_AUTHORIZE_ENTRYPOINT", OperateAbi.EventAuthorizeEntrypoint)]
    [InlineData(
        "XMIP_EVENT_BATCH_UNHEARD_ENTRYPOINT", OperateAbi.EventBatchUnheardEntrypoint)]
    [InlineData("XMIP_EVENT_UNHEARD_ENTRYPOINT", OperateAbi.EventUnheardEntrypoint)]
    [InlineData(
        "XMIP_EVENT_SUBSCRIPTIONS_ENTRYPOINT", OperateAbi.EventSubscriptionsEntrypoint)]
    [InlineData(
        "XMIP_EVENT_SUBSCRIPTION_ACT_ENTRYPOINT", OperateAbi.EventSubscriptionActEntrypoint)]
    [InlineData(
        "XMIP_PUBLICATION_EVENT_SUBSCRIPTIONS_ENTRYPOINT",
        OperateAbi.PublicationEventSubscriptionsEntrypoint)]
    [InlineData("XMIP_SUBSCRIPTIONS_ENTRYPOINT", OperateAbi.SubscriptionsEntrypoint)]
    [InlineData("XMIP_SUBSCRIPTION_ACT_ENTRYPOINT", OperateAbi.SubscriptionActEntrypoint)]
    [InlineData(
        "XMIP_PUBLICATION_SUBSCRIPTIONS_ENTRYPOINT", OperateAbi.PublicationSubscriptionsEntrypoint)]
    [InlineData("XMIP_ORDER_ENTRYPOINT", OperateAbi.OrderEntrypoint)]
    [InlineData("XMIP_DEAD_MESSAGES_ENTRYPOINT", OperateAbi.DeadMessagesEntrypoint)]
    [InlineData("XMIP_DEAD_MESSAGE_REPLAY_ENTRYPOINT", OperateAbi.DeadMessageReplayEntrypoint)]
    [InlineData(
        "XMIP_PUBLICATION_DEAD_MESSAGES_ENTRYPOINT", OperateAbi.PublicationDeadMessagesEntrypoint)]
    [InlineData("XMIP_JOURNEY_ACT_ENTRYPOINT", OperateAbi.JourneyActEntrypoint)]
    [InlineData(
        "XMIP_TECHNOLOGY_CATALOGUE_ENTRYPOINT", OperateAbi.TechnologyCatalogueEntrypoint)]
    [InlineData("XMIP_PROCESS_DECLARE_ENTRYPOINT", OperateAbi.ProcessDeclareEntrypoint)]
    [InlineData(
        "XMIP_PROCESS_DECLARATIONS_ENTRYPOINT", OperateAbi.ProcessDeclarationsEntrypoint)]
    [InlineData("XMIP_REMOTE_OPEN_ENTRYPOINT", OperateAbi.RemoteOpenEntrypoint)]
    [InlineData("XMIP_REMOTE_OPEN_KEPT_ENTRYPOINT", OperateAbi.RemoteOpenKeptEntrypoint)]
    [InlineData("XMIP_REMOTE_PUBLICATION_ENTRYPOINT", OperateAbi.RemotePublicationEntrypoint)]
    [InlineData("XMIP_REMOTE_WAIT_ENTRYPOINT", OperateAbi.RemoteWaitEntrypoint)]
    [InlineData("XMIP_REMOTE_ACT_ENTRYPOINT", OperateAbi.RemoteActEntrypoint)]
    [InlineData("XMIP_REMOTE_END_ENTRYPOINT", OperateAbi.RemoteEndEntrypoint)]
    [InlineData("XMIP_REMOTE_FREE_ENTRYPOINT", OperateAbi.RemoteFreeEntrypoint)]
    [InlineData("XMIP_OPERATE_SERVE_ENTRYPOINT", OperateAbi.OperateServeEntrypoint)]
    [InlineData(
        "XMIP_OPERATE_SERVED_ADDRESS_ENTRYPOINT", OperateAbi.OperateServedAddressEntrypoint)]
    [InlineData("XMIP_OPERATE_POST_ENTRYPOINT", OperateAbi.OperatePostEntrypoint)]
    [InlineData("XMIP_OPERATE_SERVE_STOP_ENTRYPOINT", OperateAbi.OperateServeStopEntrypoint)]
    [InlineData(
        "XMIP_SECURITY_ROLE_NAMED_ENTRYPOINT", OperateAbi.SecurityRoleNamedEntrypoint)]
    [InlineData(
        "XMIP_SECURITY_ROLE_WORDS_ENTRYPOINT", OperateAbi.SecurityRoleWordsEntrypoint)]
    [InlineData("XMIP_ACT_ADMITTED_ENTRYPOINT", OperateAbi.ActAdmittedEntrypoint)]
    [InlineData("XMIP_OPERATOR_REQUEST_ENTRYPOINT", OperateAbi.OperatorRequestEntrypoint)]
    [InlineData(
        "XMIP_OPERATOR_INFORMATION_ENTRYPOINT", OperateAbi.OperatorInformationEntrypoint)]
    [InlineData("XMIP_OPERATOR_ASSEMBLE_ENTRYPOINT", OperateAbi.OperatorAssembleEntrypoint)]
    [InlineData("XMIP_OPERATOR_KEEP_ENTRYPOINT", OperateAbi.OperatorKeepEntrypoint)]
    [InlineData("XMIP_OPERATOR_SIGN_ENTRYPOINT", OperateAbi.OperatorSignEntrypoint)]
    [InlineData("XMIP_DATABASE_UPGRADE_ENTRYPOINT", OperateAbi.DatabaseUpgradeEntrypoint)]
    [InlineData("XMIP_HOST_ISSUE_ENTRYPOINT", OperateAbi.HostIssueEntrypoint)]
    [InlineData("XMIP_LISTENER_OPEN_ENTRYPOINT", OperateAbi.ListenerOpenEntrypoint)]
    [InlineData("XMIP_LISTENER_ADDRESS_ENTRYPOINT", OperateAbi.ListenerAddressEntrypoint)]
    [InlineData("XMIP_LISTENER_ACCEPT_ENTRYPOINT", OperateAbi.ListenerAcceptEntrypoint)]
    [InlineData("XMIP_LISTENER_STOP_ENTRYPOINT", OperateAbi.ListenerStopEntrypoint)]
    [InlineData("XMIP_LISTENER_FREE_ENTRYPOINT", OperateAbi.ListenerFreeEntrypoint)]
    [InlineData("XMIP_SESSION_PEER_ENTRYPOINT", OperateAbi.SessionPeerEntrypoint)]
    [InlineData("XMIP_SESSION_ENDS_ENTRYPOINT", OperateAbi.SessionEndsEntrypoint)]
    [InlineData("XMIP_SESSION_READ_ENTRYPOINT", OperateAbi.SessionReadEntrypoint)]
    [InlineData("XMIP_SESSION_WRITE_ENTRYPOINT", OperateAbi.SessionWriteEntrypoint)]
    [InlineData("XMIP_SESSION_CLOSE_ENTRYPOINT", OperateAbi.SessionCloseEntrypoint)]
    [InlineData("XMIP_SESSION_FREE_ENTRYPOINT", OperateAbi.SessionFreeEntrypoint)]
    public void NamesEachSymbolTheHeaderDeclares(string define, string symbol)
    {
        Assert.Equal(Header.Define("xmip_operate.h", define), symbol);
    }

    [Fact]
    public void NamesEachProtectionTheHeaderDefines()
    {
        Assert.Equal(
            Header.Define("xmip_operate.h", "XMIP_PROTECTION_TLS"),
            ((int)Protection.Tls).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void KnowsEveryHealthMoodTheHeaderDefinesAndNoOther()
    {
        IReadOnlyDictionary<string, int> header = Header.Enumerators("HEALTH");

        Assert.NotEmpty(header);

        foreach ((string constant, int value) in header)
        {
            Assert.Equal(Header.MemberName(constant), ((HealthState)value).ToString());
        }

        Assert.Equal(header.Count, Enum.GetValues<HealthState>().Length);
    }

    [Fact]
    public void KnowsEveryCountedKindTheHeaderDefinesAndNoOther()
    {
        IReadOnlyDictionary<string, int> header = Header.Enumerators("COUNTED");

        Assert.NotEmpty(header);

        foreach ((string constant, int value) in header)
        {
            Assert.Equal(Header.MemberName(constant), ((Counted)value).ToString());
        }

        Assert.Equal(header.Count, Enum.GetValues<Counted>().Length);
    }

    [Theory]
    [InlineData("TOPOLOGY", typeof(TopologyNodeKind))]
    [InlineData("ORIGIN", typeof(TopologyOrigin))]
    [InlineData("PATTERN", typeof(CommunicationPattern))]
    [InlineData("RUN", typeof(RunList))]
    public void KnowsEverySectionEightValueTheHeaderDefinesAndNoOther(string family, Type values)
    {
        IReadOnlyDictionary<string, int> header = Header.Enumerators(family);

        Assert.NotEmpty(header);

        foreach ((string constant, int value) in header)
        {
            Assert.Equal(Header.MemberName(constant), Enum.GetName(values, value));
        }

        Assert.Equal(header.Count, Enum.GetValues(values).Length);
    }

    [Theory]
    [InlineData("PHASE", typeof(AuditPhase))]
    [InlineData("SEVERITY", typeof(AuditSeverity))]
    [InlineData("KEPT", typeof(AuditKept))]
    public void KnowsEverySectionNineValueTheHeaderDefinesAndNoOther(string family, Type values)
    {
        IReadOnlyDictionary<string, int> header = Header.Enumerators(family);

        Assert.NotEmpty(header);

        foreach ((string constant, int value) in header)
        {
            Assert.Equal(Header.MemberName(constant), Enum.GetName(values, value));
        }

        Assert.Equal(header.Count, Enum.GetValues(values).Length);
    }

    [Theory]
    [InlineData("ACTION", typeof(EventAction))]
    [InlineData("OUTCOME", typeof(EventOutcome))]
    public void KnowsEverySectionElevenValueTheHeaderDefinesAndNoOther(
        string family, Type values)
    {
        IReadOnlyDictionary<string, int> header = Header.Enumerators(family);

        Assert.NotEmpty(header);

        foreach ((string constant, int value) in header)
        {
            Assert.Equal(Header.MemberName(constant), Enum.GetName(values, value));
        }

        Assert.Equal(header.Count, Enum.GetValues(values).Length);
    }
}
