using Xmip.Abi.Operate;

namespace Xmip.Surface;

/// <summary>
/// One scope of a <see cref="ScopeIndex"/> and everything the index knows
/// about it: the records at or beneath it worst first, its worst, its own,
/// its children, its sums and the stage it is on. Made and filled only while
/// the index is built; read-only once it is.
/// </summary>
internal sealed class ScopeEntry
{
    // Where the record standing as this scope's own was published.
    private int ownAt = -1;

    /// <summary>A scope beneath <paramref name="parent"/>, or the root where
    /// there is none.</summary>
    public ScopeEntry(string scope, string label, ScopeEntry? parent)
    {
        Scope = scope;
        Label = label;
        Parent = parent;

        if (parent is not null)
        {
            parent.Children.Add(this);

            // Where a stage begins: on one, beneath one on none (ScopeTree.Stage).
            Stage = ScopeTree.Stage(scope);
            BeginsStage = Stage.Length > 0 && parent.Stage.Length == 0;
        }
    }

    public string Scope { get; }

    public string Label { get; }

    public ScopeEntry? Parent { get; }

    /// <summary>The stage of the message path this scope is on, as observe
    /// reads it; empty for none.</summary>
    public string Stage { get; } = string.Empty;

    public bool BeginsStage { get; }

    /// <summary>Every record at or beneath this scope, worst first.</summary>
    public List<HealthRecord> Leaves { get; } = [];

    public List<ScopeEntry> Children { get; } = [];

    public HealthRecord? Worst { get; private set; }

    public HealthRecord? Own { get; private set; }

    public ulong?[] Sums { get; } = new ulong?[6];

    public bool IsLeaf => Children.Count == 0;

    public HealthState? Mood =>
        Worst is null ? null : IsLeaf ? Worst.State : ScopeTree.Rolled(Worst.State);

    public Branch Branch => new(
        Scope,
        Label,
        Mood ?? HealthState.Fine,
        IsLeaf,
        Leaves.Count,
        Worst ?? new HealthRecord(
            Scope, HealthState.Fine, 0, string.Empty, DateTimeOffset.MinValue));

    /// <summary>This scope as the record it stands as in the worst-first
    /// order: its worst leaf's mood and severity under its own label, and a
    /// scope with nothing beneath it as a Fine one.</summary>
    public HealthRecord Standing => new(
        Label, Worst?.State ?? HealthState.Fine, Worst?.Severity ?? 0, string.Empty, default);

    /// <summary>Hold a record at or beneath this scope; they come worst first,
    /// so the first held is the worst.</summary>
    public ScopeEntry Holding(HealthRecord record)
    {
        Leaves.Add(record);
        Worst ??= record;

        return this;
    }

    /// <summary>The record at exactly this scope, published at
    /// <paramref name="at"/>: the last published stands.</summary>
    public void Owning(HealthRecord record, int at)
    {
        if (at > ownAt)
        {
            Own = record;
            ownAt = at;
        }
    }
}
