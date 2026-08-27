namespace Plugin.Maui.OfflineSync;

/// <summary>
/// Persistence contract for documents, pending changes, and collection cursors.
/// </summary>
public interface ISyncStore : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<SyncDocument?> GetAsync(string collection, string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SyncDocument>> GetAllAsync(string collection, bool includeDeleted = false, CancellationToken cancellationToken = default);

    Task UpsertLocalAsync(SyncDocument document, ChangeOperation operation, CancellationToken cancellationToken = default);

    Task ApplyRemoteAsync(SyncDocument document, CancellationToken cancellationToken = default);

    Task RemoveAsync(string collection, string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingChange>> GetPendingChangesAsync(string? collection = null, int? take = null, CancellationToken cancellationToken = default);

    Task MarkChangeSyncedAsync(PendingChange change, long remoteVersion, DateTimeOffset remoteUpdatedAt, string? payloadJson = null, CancellationToken cancellationToken = default);

    Task MarkChangeFailedAsync(PendingChange change, string error, bool permanentlyFailed, CancellationToken cancellationToken = default);

    Task DiscardPendingChangeAsync(PendingChange change, CancellationToken cancellationToken = default);

    Task RequeueFailedAsync(string? collection = null, CancellationToken cancellationToken = default);

    Task<string?> GetCursorAsync(string collection, CancellationToken cancellationToken = default);

    Task SetCursorAsync(string collection, string? cursor, CancellationToken cancellationToken = default);

    Task<int> GetPendingCountAsync(string? collection = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetKnownCollectionsAsync(CancellationToken cancellationToken = default);
}
