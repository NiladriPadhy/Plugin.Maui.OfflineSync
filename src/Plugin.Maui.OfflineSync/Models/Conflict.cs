namespace Plugin.Maui.OfflineSync;

/// <summary>
/// Inputs provided to <see cref="IConflictResolver"/>.
/// </summary>
public sealed class ConflictContext
{
    public required string Collection { get; init; }

    public required string EntityId { get; init; }

    public required string LocalJson { get; init; }

    public required string RemoteJson { get; init; }

    public DateTimeOffset LocalUpdatedAtUtc { get; init; }

    public DateTimeOffset RemoteUpdatedAtUtc { get; init; }

    public long LocalVersion { get; init; }

    public long RemoteVersion { get; init; }

    public bool LocalIsDeleted { get; init; }

    public bool RemoteIsDeleted { get; init; }
}

/// <summary>
/// Result of conflict resolution. Use <see cref="ConflictWinner.Merged"/> with <see cref="MergedJson"/>
/// to keep a combined document that will be pushed as a local update.
/// </summary>
public sealed class ConflictResolution
{
    public required ConflictWinner Winner { get; init; }

    public string? MergedJson { get; init; }

    public static ConflictResolution Local() => new() { Winner = ConflictWinner.Local };

    public static ConflictResolution Remote() => new() { Winner = ConflictWinner.Remote };

    public static ConflictResolution Merged(string json) => new() { Winner = ConflictWinner.Merged, MergedJson = json };
}
