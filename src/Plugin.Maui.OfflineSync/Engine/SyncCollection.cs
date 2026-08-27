namespace Plugin.Maui.OfflineSync;

internal sealed class SyncCollection<T> : ISyncCollection<T> where T : SyncableEntity, new()
{
    private readonly OfflineSyncEngine _engine;
    private readonly ISyncStore _store;
    private readonly OfflineSyncOptions _options;

    public SyncCollection(string name, OfflineSyncEngine engine, ISyncStore store, OfflineSyncOptions options)
    {
        Name = name;
        _engine = engine;
        _store = store;
        _options = options;
    }

    public string Name { get; }

    public async Task<T> InsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _engine.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = Guid.NewGuid().ToString("N");
        }

        var now = DateTimeOffset.UtcNow;
        entity.CreatedAtUtc = now;
        entity.UpdatedAtUtc = now;
        entity.IsDeleted = false;
        entity.SyncState = SyncState.PendingCreate;

        await _store.UpsertLocalAsync(EntityMapper.ToDocument(Name, entity), ChangeOperation.Insert, cancellationToken).ConfigureAwait(false);
        _engine.NotifyCollectionChanged(Name, entity.Id, ChangeOperation.Insert, fromRemote: false);
        return entity;
    }

    public async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _engine.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        await _store.UpsertLocalAsync(EntityMapper.ToDocument(Name, entity), ChangeOperation.Update, cancellationToken).ConfigureAwait(false);

        var stored = await _store.GetAsync(Name, entity.Id, cancellationToken).ConfigureAwait(false);
        if (stored is not null)
        {
            entity.SyncState = stored.SyncState;
            entity.Version = stored.Version;
        }

        _engine.NotifyCollectionChanged(Name, entity.Id, ChangeOperation.Update, fromRemote: false);
        return entity;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await _engine.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var existing = await _store.GetAsync(Name, id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return;
        }

        if (!_options.UseSoftDelete && existing.SyncState == SyncState.PendingCreate)
        {
            await _store.RemoveAsync(Name, id, cancellationToken).ConfigureAwait(false);
            _engine.NotifyCollectionChanged(Name, id, ChangeOperation.Delete, fromRemote: false);
            return;
        }

        existing.IsDeleted = true;
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _store.UpsertLocalAsync(existing, ChangeOperation.Delete, cancellationToken).ConfigureAwait(false);
        _engine.NotifyCollectionChanged(Name, id, ChangeOperation.Delete, fromRemote: false);
    }

    public async Task<T?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        await _engine.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var document = await _store.GetAsync(Name, id, cancellationToken).ConfigureAwait(false);
        return document is null || document.IsDeleted ? null : EntityMapper.ToEntity<T>(document);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _engine.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var documents = await _store.GetAllAsync(Name, includeDeleted: false, cancellationToken).ConfigureAwait(false);
        return documents.Select(EntityMapper.ToEntity<T>).ToList();
    }

    public async Task<IReadOnlyList<T>> QueryAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var all = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.Where(predicate).ToList();
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken).ConfigureAwait(false);
        return all.Count;
    }
}
