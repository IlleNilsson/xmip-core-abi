namespace Xmip.Abi.Operate;

/// <summary>
/// Each entry a section 8 read lends — a health record, a measurement, a
/// topology node and a communication link — copied out of the runtime's
/// borrow into the record .NET keeps. Unsafe, each: the caller promises the
/// borrow is live.
/// </summary>
internal static class PublicationEntries
{
    internal static unsafe HealthRecord Record(XmipHealthEntry entry)
    {
        unsafe
        {
            return new HealthRecord(
                entry.Scope.Read(),
                (HealthState)entry.Health,
                entry.Severity,
                entry.Evidence.Read(),
                Operator.FromNanos(entry.ObservedUnixNanos));
        }
    }

    internal static unsafe MeasurementRecord Count(XmipMeasurement entry)
    {
        unsafe
        {
            return new MeasurementRecord(
                entry.Scope.Read(),
                (Counted)entry.Counted,
                entry.Value,
                Operator.FromNanos(entry.WindowStartUnixNanos),
                Operator.FromNanos(entry.WindowEndUnixNanos),
                Operator.FromNanos(entry.ObservedUnixNanos));
        }
    }

    internal static unsafe TopologyNode Node(XmipTopologyNode entry)
    {
        unsafe
        {
            string parent = entry.Parent.Read();

            return new TopologyNode(
                entry.Id.Read(),
                parent.Length > 0 ? parent : null,
                entry.Label.Read(),
                (TopologyNodeKind)entry.Kind,
                entry.Scope.Read(),
                (HealthState)entry.Health,
                (TopologyOrigin)entry.Origin,
                entry.Load,
                entry.Activity,
                entry.Evidence.Read());
        }
    }

    internal static unsafe CommunicationLink Link(XmipTopologyLink entry)
    {
        unsafe
        {
            return new CommunicationLink(
                entry.Id.Read(),
                entry.From.Read(),
                entry.To.Read(),
                (CommunicationPattern)entry.Pattern,
                (TopologyOrigin)entry.Origin,
                entry.Protocol.Read(),
                (HealthState)entry.Health,
                entry.Volume,
                entry.Rate,
                entry.LatencyMilliseconds,
                entry.Progress,
                entry.Attempts,
                entry.Evidence.Read());
        }
    }
}
