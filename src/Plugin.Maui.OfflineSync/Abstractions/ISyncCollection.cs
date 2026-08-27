namespace Plugin.Maui.OfflineSync;

/// <summary>
/// Typed local-first collection. Writes are persisted immediately and queued for later sync.
/// </summary>
public interface ISyncCollection<T> where T : SyncableEntity, new()
{
    /// <summary>Collection name used as the remote resource and local partition key.</summary>
    string Name { get; }

    Task<T> InsertAsync(T entity, CancellationToken cancellationToken = default);

    Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<T?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> QueryAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
