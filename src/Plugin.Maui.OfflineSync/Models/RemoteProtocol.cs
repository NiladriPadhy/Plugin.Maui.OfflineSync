namespace Plugin.Maui.OfflineSync;

/// <summary>
/// A single outgoing change sent to the remote.
/// </summary>
public sealed class PushChange
{
    public required string Id { get; init; }

    public required ChangeOperation Operation { get; init; }

    public long BaseVersion { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public required string PayloadJson { get; init; }

    public bool Force { get; init; }
}

/// <summary>
/// Remote acknowledgement for a push batch.
/// </summary>
public sealed class PushResponse
{
    public List<AcceptedChange> Accepted { get; init; } = [];

    public List<RemoteConflict> Conflicts { get; init; } = [];

    public List<RejectedChange> Rejected { get; init; } = [];
}

public sealed class AcceptedChange
{
    public required string Id { get; init; }

    public long Version { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class RejectedChange
{
    public required string Id { get; init; }

    public string? Error { get; init; }
}

public sealed class RemoteConflict
{
    public required string Id { get; init; }

    public long ServerVersion { get; init; }

    public DateTimeOffset ServerUpdatedAtUtc { get; init; }

    public bool ServerIsDeleted { get; init; }

    public required string ServerPayloadJson { get; init; }
}

/// <summary>
/// Incremental pull payload from the remote.
/// </summary>
public sealed class PullResponse
{
    public List<RemoteDocument> Items { get; init; } = [];

    public string? Cursor { get; init; }

    public DateTimeOffset ServerTimeUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class RemoteDocument
{
    public required string Id { get; init; }

    public long Version { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public bool IsDeleted { get; init; }

    public required string PayloadJson { get; init; }
}
