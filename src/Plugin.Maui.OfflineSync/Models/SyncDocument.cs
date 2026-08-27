namespace Plugin.Maui.OfflineSync;

/// <summary>
/// Stored representation of a synchronized document.
/// </summary>
public sealed class SyncDocument
{
    public required string Collection { get; init; }

    public required string Id { get; init; }

    public required string PayloadJson { get; set; }

    public long Version { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public bool IsDeleted { get; set; }

    public SyncState SyncState { get; set; }
}

/// <summary>
/// Queued local mutation waiting to be pushed.
/// </summary>
public sealed class PendingChange
{
    public long ChangeId { get; init; }

    public required string Collection { get; init; }

    public required string EntityId { get; init; }

    public ChangeOperation Operation { get; init; }

    public required string PayloadJson { get; init; }

    public long BaseVersion { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public int AttemptCount { get; init; }

    public string? LastError { get; init; }

    public bool Force { get; init; }

    public bool IsFailed { get; init; }
}
