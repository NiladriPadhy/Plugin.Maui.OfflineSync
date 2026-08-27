namespace Plugin.Maui.OfflineSync;

public sealed class SyncStatusChangedEventArgs : EventArgs
{
    public required SyncStatus Status { get; init; }

    public SyncStatus Previous { get; init; }
}

public sealed class SyncCompletedEventArgs : EventArgs
{
    public required SyncResult Result { get; init; }

    public string? Collection { get; init; }
}

public sealed class ConflictDetectedEventArgs : EventArgs
{
    public required string Collection { get; init; }

    public required string EntityId { get; init; }

    public required ConflictWinner Winner { get; init; }
}

public sealed class CollectionChangedEventArgs : EventArgs
{
    public required string Collection { get; init; }

    public required string EntityId { get; init; }

    public required ChangeOperation Operation { get; init; }

    public bool FromRemote { get; init; }
}
