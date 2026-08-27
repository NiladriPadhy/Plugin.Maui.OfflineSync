namespace Plugin.Maui.OfflineSync.Remote;

/// <summary>
/// Process-local remote used for tests and demos. Detects version conflicts like a real server.
/// </summary>
public sealed class InMemoryRemoteSyncClient : IRemoteSyncClient
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, RemoteDocument>> _collections = new();
    private readonly object _gate = new();

    public Task<PullResponse> PullAsync(string collection, string? cursor, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var items = GetCollection(collection).Values.ToList();
        if (TryParseCursor(cursor, out var since))
        {
            items = items.Where(item => item.UpdatedAtUtc > since).ToList();
        }

        var serverTime = DateTimeOffset.UtcNow;
        return Task.FromResult(new PullResponse
        {
            Items = items,
            Cursor = serverTime.UtcTicks.ToString(),
            ServerTimeUtc = serverTime
        });
    }

    public Task<PushResponse> PushAsync(string collection, IReadOnlyList<PushChange> changes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var store = GetCollection(collection);
        var response = new PushResponse();

        lock (_gate)
        {
            foreach (var change in changes)
            {
                store.TryGetValue(change.Id, out var existing);

                if (existing is not null && existing.Version != change.BaseVersion && !change.Force)
                {
                    response.Conflicts.Add(new RemoteConflict
                    {
                        Id = change.Id,
                        ServerVersion = existing.Version,
                        ServerUpdatedAtUtc = existing.UpdatedAtUtc,
                        ServerIsDeleted = existing.IsDeleted,
                        ServerPayloadJson = existing.PayloadJson
                    });
                    continue;
                }

                var now = DateTimeOffset.UtcNow;
                var accepted = new RemoteDocument
                {
                    Id = change.Id,
                    Version = (existing?.Version ?? change.BaseVersion) + 1,
                    UpdatedAtUtc = now,
                    CreatedAtUtc = existing?.CreatedAtUtc ?? now,
                    IsDeleted = change.Operation == ChangeOperation.Delete,
                    PayloadJson = change.PayloadJson
                };

                store[change.Id] = accepted;
                response.Accepted.Add(new AcceptedChange
                {
                    Id = accepted.Id,
                    Version = accepted.Version,
                    UpdatedAtUtc = accepted.UpdatedAtUtc
                });
            }
        }

        return Task.FromResult(response);
    }

    /// <summary>
    /// Seeds or overwrites a remote document. Useful for conflict tests.
    /// </summary>
    public void Seed(string collection, RemoteDocument document)
    {
        GetCollection(collection)[document.Id] = document;
    }

    public RemoteDocument? Get(string collection, string id) =>
        GetCollection(collection).TryGetValue(id, out var document) ? document : null;

    private ConcurrentDictionary<string, RemoteDocument> GetCollection(string collection) =>
        _collections.GetOrAdd(collection, _ => new ConcurrentDictionary<string, RemoteDocument>(StringComparer.OrdinalIgnoreCase));

    private static bool TryParseCursor(string? cursor, out DateTimeOffset since)
    {
        if (long.TryParse(cursor, out var ticks))
        {
            since = new DateTimeOffset(ticks, TimeSpan.Zero);
            return true;
        }

        since = default;
        return false;
    }
}
