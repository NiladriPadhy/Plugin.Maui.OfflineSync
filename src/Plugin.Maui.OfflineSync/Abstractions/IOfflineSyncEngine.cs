namespace Plugin.Maui.OfflineSync;

/// <summary>
/// Coordinates local collections, pending change queues, and remote synchronization.
/// </summary>
public interface IOfflineSyncEngine : IAsyncDisposable
{
    /// <summary>Current high-level engine status.</summary>
    SyncStatus Status { get; }

    /// <summary>Whether the network monitor currently reports connectivity.</summary>
    bool IsOnline { get; }

    /// <summary>Raised whenever <see cref="Status"/> changes.</summary>
    event EventHandler<SyncStatusChangedEventArgs>? StatusChanged;

    /// <summary>Raised after a sync cycle finishes (success, skip, or failure).</summary>
    event EventHandler<SyncCompletedEventArgs>? SyncCompleted;

    /// <summary>Raised when a local/remote conflict is detected and resolved.</summary>
    event EventHandler<ConflictDetectedEventArgs>? ConflictDetected;

    /// <summary>Raised after a local collection mutates or remote items are merged.</summary>
    event EventHandler<CollectionChangedEventArgs>? CollectionChanged;

    /// <summary>
    /// Returns a typed collection. The default name is the type name.
    /// </summary>
    ISyncCollection<T> GetCollection<T>(string? name = null) where T : SyncableEntity, new();

    /// <summary>Pushes pending local changes and pulls remote updates for every known collection.</summary>
    Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default);

    /// <summary>Synchronizes a single collection.</summary>
    Task<SyncResult> SyncCollectionAsync(string collection, CancellationToken cancellationToken = default);

    /// <summary>Starts periodic, connectivity, and optional background sync.</summary>
    Task StartAutoSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops automatic synchronization.</summary>
    Task StopAutoSyncAsync();

    /// <summary>Number of queued local changes waiting to be pushed.</summary>
    Task<int> GetPendingCountAsync(string? collection = null, CancellationToken cancellationToken = default);

    /// <summary>Moves failed changes back into the pending queue so they can be retried.</summary>
    Task RequeueFailedAsync(string? collection = null, CancellationToken cancellationToken = default);
}
