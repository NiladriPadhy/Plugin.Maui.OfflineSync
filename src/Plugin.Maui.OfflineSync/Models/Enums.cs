namespace Plugin.Maui.OfflineSync;

/// <summary>
/// Local synchronization state of a document.
/// </summary>
public enum SyncState
{
    Synced = 0,
    PendingCreate = 1,
    PendingUpdate = 2,
    PendingDelete = 3,
    Conflict = 4,
    Failed = 5
}

/// <summary>
/// Mutation recorded in the pending change log.
/// </summary>
public enum ChangeOperation
{
    Insert = 0,
    Update = 1,
    Delete = 2
}

/// <summary>
/// Built-in conflict strategies.
/// </summary>
public enum ConflictStrategy
{
    LastWriteWins = 0,
    ServerWins = 1,
    ClientWins = 2,
    Custom = 3
}

/// <summary>
/// High-level engine status.
/// </summary>
public enum SyncStatus
{
    Idle = 0,
    Syncing = 1,
    Offline = 2,
    Failed = 3
}

/// <summary>
/// Which document should be kept after a conflict.
/// </summary>
public enum ConflictWinner
{
    Local = 0,
    Remote = 1,
    Merged = 2
}
