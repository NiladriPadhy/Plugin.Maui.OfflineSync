namespace Plugin.Maui.OfflineSync.Storage;

/// <summary>
/// Thread-safe in-memory store used by tests and optional demos.
/// </summary>
public sealed class InMemorySyncStore : ISyncStore
{
    private readonly ConcurrentDictionary<string, SyncDocument> _documents = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PendingChange> _changes = [];
    private readonly ConcurrentDictionary<string, string?> _cursors = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private long _nextChangeId = 1;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<SyncDocument?> GetAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        _documents.TryGetValue(Key(collection, id), out var document);
        return Task.FromResult(document is null ? null : Clone(document));
    }

    public Task<IReadOnlyList<SyncDocument>> GetAllAsync(string collection, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var items = _documents.Values
            .Where(document => string.Equals(document.Collection, collection, StringComparison.OrdinalIgnoreCase))
            .Where(document => includeDeleted || !document.IsDeleted)
            .Select(Clone)
            .ToList();
        return Task.FromResult<IReadOnlyList<SyncDocument>>(items);
    }

    public Task UpsertLocalAsync(SyncDocument document, ChangeOperation operation, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _documents.TryGetValue(Key(document.Collection, document.Id), out var existing);
            var plan = ChangeCoalescer.Plan(existing, Clone(document), operation);

            if (plan.DropExistingPending)
            {
                _changes.RemoveAll(change =>
                    string.Equals(change.Collection, document.Collection, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(change.EntityId, document.Id, StringComparison.OrdinalIgnoreCase));
            }

            if (plan.RemoveDocument)
            {
                _documents.TryRemove(Key(document.Collection, document.Id), out _);
                return Task.CompletedTask;
            }

            if (plan.Document is not null)
            {
                _documents[Key(plan.Document.Collection, plan.Document.Id)] = Clone(plan.Document);
            }

            if (plan.Mutation is not null)
            {
                _changes.Add(new PendingChange
                {
                    ChangeId = _nextChangeId++,
                    Collection = document.Collection,
                    EntityId = document.Id,
                    Operation = plan.Mutation.Operation,
                    PayloadJson = plan.Mutation.PayloadJson,
                    BaseVersion = plan.Mutation.BaseVersion,
                    CreatedAtUtc = plan.Mutation.CreatedAtUtc
                });
            }
        }

        return Task.CompletedTask;
    }

    public Task ApplyRemoteAsync(SyncDocument document, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _changes.RemoveAll(change =>
                string.Equals(change.Collection, document.Collection, StringComparison.OrdinalIgnoreCase)
                && string.Equals(change.EntityId, document.Id, StringComparison.OrdinalIgnoreCase));

            var stored = Clone(document);
            stored.SyncState = document.IsDeleted ? SyncState.Synced : SyncState.Synced;
            _documents[Key(document.Collection, document.Id)] = stored;
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _documents.TryRemove(Key(collection, id), out _);
            _changes.RemoveAll(change =>
                string.Equals(change.Collection, collection, StringComparison.OrdinalIgnoreCase)
                && string.Equals(change.EntityId, id, StringComparison.OrdinalIgnoreCase));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PendingChange>> GetPendingChangesAsync(string? collection = null, int? take = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IEnumerable<PendingChange> query = _changes.Where(change => !change.IsFailed);

            if (!string.IsNullOrWhiteSpace(collection))
            {
                query = query.Where(change => string.Equals(change.Collection, collection, StringComparison.OrdinalIgnoreCase));
            }

            query = query.OrderBy(change => change.CreatedAtUtc);
            if (take is > 0)
            {
                query = query.Take(take.Value);
            }

            return Task.FromResult<IReadOnlyList<PendingChange>>(query.ToList());
        }
    }

    public Task MarkChangeSyncedAsync(PendingChange change, long remoteVersion, DateTimeOffset remoteUpdatedAt, string? payloadJson = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _changes.RemoveAll(item => item.ChangeId == change.ChangeId);

            if (_documents.TryGetValue(Key(change.Collection, change.EntityId), out var document))
            {
                document.Version = remoteVersion;
                document.UpdatedAtUtc = remoteUpdatedAt;
                document.SyncState = SyncState.Synced;
                if (payloadJson is not null)
                {
                    document.PayloadJson = payloadJson;
                }

                if (change.Operation == ChangeOperation.Delete)
                {
                    document.IsDeleted = true;
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task MarkChangeFailedAsync(PendingChange change, string error, bool permanentlyFailed, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var index = _changes.FindIndex(item => item.ChangeId == change.ChangeId);
            if (index < 0)
            {
                return Task.CompletedTask;
            }

            var current = _changes[index];
            _changes[index] = new PendingChange
            {
                ChangeId = current.ChangeId,
                Collection = current.Collection,
                EntityId = current.EntityId,
                Operation = current.Operation,
                PayloadJson = current.PayloadJson,
                BaseVersion = current.BaseVersion,
                CreatedAtUtc = current.CreatedAtUtc,
                AttemptCount = current.AttemptCount + 1,
                LastError = error,
                Force = current.Force,
                IsFailed = permanentlyFailed
            };

            if (permanentlyFailed && _documents.TryGetValue(Key(change.Collection, change.EntityId), out var document))
            {
                document.SyncState = SyncState.Failed;
            }
        }

        return Task.CompletedTask;
    }

    public Task DiscardPendingChangeAsync(PendingChange change, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _changes.RemoveAll(item => item.ChangeId == change.ChangeId);
        }

        return Task.CompletedTask;
    }

    public Task RequeueFailedAsync(string? collection = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            for (var i = 0; i < _changes.Count; i++)
            {
                var change = _changes[i];
                if (!change.IsFailed)
                {
                    continue;
                }

                if (collection is not null && !string.Equals(change.Collection, collection, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _changes[i] = new PendingChange
                {
                    ChangeId = change.ChangeId,
                    Collection = change.Collection,
                    EntityId = change.EntityId,
                    Operation = change.Operation,
                    PayloadJson = change.PayloadJson,
                    BaseVersion = change.BaseVersion,
                    CreatedAtUtc = change.CreatedAtUtc,
                    AttemptCount = 0,
                    LastError = null,
                    Force = change.Force,
                    IsFailed = false
                };

                if (_documents.TryGetValue(Key(change.Collection, change.EntityId), out var document))
                {
                    document.SyncState = change.Operation switch
                    {
                        ChangeOperation.Insert => SyncState.PendingCreate,
                        ChangeOperation.Delete => SyncState.PendingDelete,
                        _ => SyncState.PendingUpdate
                    };
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetCursorAsync(string collection, CancellationToken cancellationToken = default) =>
        Task.FromResult(_cursors.TryGetValue(collection, out var cursor) ? cursor : null);

    public Task SetCursorAsync(string collection, string? cursor, CancellationToken cancellationToken = default)
    {
        _cursors[collection] = cursor;
        return Task.CompletedTask;
    }

    public Task<int> GetPendingCountAsync(string? collection = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var count = _changes.Count(change =>
                !change.IsFailed
                && (collection is null || string.Equals(change.Collection, collection, StringComparison.OrdinalIgnoreCase)));
            return Task.FromResult(count);
        }
    }

    public Task<IReadOnlyList<string>> GetKnownCollectionsAsync(CancellationToken cancellationToken = default)
    {
        var names = _documents.Values
            .Select(document => document.Collection)
            .Concat(_cursors.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(names);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string Key(string collection, string id) => $"{collection}:{id}";

    private static SyncDocument Clone(SyncDocument document) =>
        new()
        {
            Collection = document.Collection,
            Id = document.Id,
            PayloadJson = document.PayloadJson,
            Version = document.Version,
            CreatedAtUtc = document.CreatedAtUtc,
            UpdatedAtUtc = document.UpdatedAtUtc,
            IsDeleted = document.IsDeleted,
            SyncState = document.SyncState
        };
}
