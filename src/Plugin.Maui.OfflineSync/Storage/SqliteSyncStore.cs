using SQLite;

namespace Plugin.Maui.OfflineSync.Storage;

/// <summary>
/// SQLite-backed document store and change log.
/// </summary>
public sealed class SqliteSyncStore : ISyncStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SQLiteAsyncConnection? _connection;

    public SqliteSyncStore(OfflineSyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _path = ResolvePath(options);
    }

    public SqliteSyncStore(string databasePath)
    {
        _path = databasePath;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is not null)
            {
                return;
            }

            SQLitePCL.Batteries_V2.Init();
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _connection = new SQLiteAsyncConnection(_path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
            await _connection.CreateTableAsync<SyncDocumentRecord>().ConfigureAwait(false);
            await _connection.CreateTableAsync<SyncChangeRecord>().ConfigureAwait(false);
            await _connection.CreateTableAsync<SyncCollectionMetaRecord>().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SyncDocument?> GetAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        var db = await GetDbAsync(cancellationToken).ConfigureAwait(false);
        var record = await db.FindAsync<SyncDocumentRecord>(SyncDocumentRecord.MakeKey(collection, id)).ConfigureAwait(false);
        return record?.ToDocument();
    }

    public async Task<IReadOnlyList<SyncDocument>> GetAllAsync(string collection, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var db = await GetDbAsync(cancellationToken).ConfigureAwait(false);
        var records = includeDeleted
            ? await db.Table<SyncDocumentRecord>().Where(record => record.Collection == collection).ToListAsync().ConfigureAwait(false)
            : await db.Table<SyncDocumentRecord>().Where(record => record.Collection == collection && !record.IsDeleted).ToListAsync().ConfigureAwait(false);

        return records.Select(record => record.ToDocument()).ToList();
    }

    public async Task UpsertLocalAsync(SyncDocument document, ChangeOperation operation, CancellationToken cancellationToken = default)
    {
        var db = await GetDbAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = (await db.FindAsync<SyncDocumentRecord>(SyncDocumentRecord.MakeKey(document.Collection, document.Id)).ConfigureAwait(false))?.ToDocument();
            var plan = ChangeCoalescer.Plan(existing, document, operation);

            if (plan.DropExistingPending)
            {
                await db.ExecuteAsync(
                    "DELETE FROM SyncChangeRecord WHERE Collection = ? AND EntityId = ?",
                    document.Collection,
                    document.Id).ConfigureAwait(false);
            }

            if (plan.RemoveDocument)
            {
                await db.DeleteAsync<SyncDocumentRecord>(SyncDocumentRecord.MakeKey(document.Collection, document.Id)).ConfigureAwait(false);
                return;
            }

            if (plan.Document is not null)
            {
                await db.InsertOrReplaceAsync(SyncDocumentRecord.FromDocument(plan.Document)).ConfigureAwait(false);
            }

            if (plan.Mutation is not null)
            {
                await db.InsertAsync(new SyncChangeRecord
                {
                    Collection = document.Collection,
                    EntityId = document.Id,
                    Operation = (int)plan.Mutation.Operation,
                    PayloadJson = plan.Mutation.PayloadJson,
                    BaseVersion = plan.Mutation.BaseVersion,
                    CreatedAtUtc = plan.Mutation.CreatedAtUtc.ToString("O")
                }).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ApplyRemoteAsync(SyncDocument document, CancellationToken cancellationToken = default)
    {
        var db = await GetDbAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await db.ExecuteAsync(
                "DELETE FROM SyncChangeRecord WHERE Collection = ? AND EntityId = ?",
                document.Collection,
                document.Id).ConfigureAwait(false);

            document.SyncState = SyncState.Synced;
            await db.InsertOrReplaceAsync(SyncDocumentRecord.FromDocument(document)).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(string collection, string id, CancellationToken cancellationToken = default)
    {
        var db = await GetDbAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await db.DeleteAsync<SyncDocumentRecord>(SyncDocumentRecord.MakeKey(collection, id)).ConfigureAwait(false);
            await db.ExecuteAsync(
                "DELETE FROM SyncChangeRecord WHERE Collection = ? AND EntityId = ?",
                collection,
                id).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<PendingChange>> GetPendingChangesAsync(string? collection = null, int? take = null, CancellationToken cancellationToken = default)
    {
        var db = await GetDbAsync(cancellationToken).ConfigureAwait(false);
        var query = db.Table<SyncChangeRecord>().Where(record => !record.IsFailed);
        if (!string.IsNullOrWhiteSpace(collection))
        {
            query = query.Where(record => record.Collection == collection);
        }

        var records = await query.OrderBy(record => record.Id).ToListAsync().ConfigureAwait(false);
        IEnumerable<SyncChangeRecord> items = records;
        if (take is > 0)
        {
            items = items.Take(take.Value);
        }

        return items.Select(record => record.ToChange()).ToList();
    }

    public async Task MarkChangeSyncedAsync(PendingChange change, long remoteVersion, DateTimeOffset remoteUpdatedAt, string? payloadJson = null, CancellationToken cancellationToken = default)
    {
        var db = await GetDbAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await db.DeleteAsync<SyncChangeRecord>((int)change.ChangeId).ConfigureAwait(false);
            var record = await db.FindAsync<SyncDocumentRecord>(SyncDocumentRecord.MakeKey(change.Collection, change.EntityId)).ConfigureAwait(false);
            if (record is null)
            {
                return;
            }

            record.Version = remoteVersion;
            record.UpdatedAtUtc = remoteUpdatedAt.ToString("O");
            record.SyncStateValue = (int)SyncState.Synced;
            if (payloadJson is not null)
            {
                record.PayloadJson = payloadJson;
            }

            if (change.Operation == ChangeOperation.Delete)
            {
                record.IsDeleted = true;
            }

            await db.UpdateAsync(record).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkChangeFailedAsync(PendingChange change, string error, bool permanentlyFailed, CancellationToken cancellationToken = default)
    {
        var db = await GetDbAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var record = await db.FindAsync<SyncChangeRecord>((int)change.ChangeId).ConfigureAwait(false);
            if (record is null)
            {
                return;
            }

            record.AttemptCount += 1;
            record.LastError = error;
            record.IsFailed = permanentlyFailed;
            await db.UpdateAsync(record).ConfigureAwait(false);

            if (permanentlyFailed)
            {
                var document = await db.FindAsync<SyncDocumentRecord>(SyncDocumentRecord.MakeKey(change.Collection, change.EntityId)).ConfigureAwait(false);
                if (document is not null)
                {
                    document.SyncStateValue = (int)SyncState.Failed;
                    await db.UpdateAsync(document).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DiscardPendingChangeAsync(PendingChange change, CancellationToken cancellationToken = default)
    {
        var db = await GetDbAsync(cancellationToken).ConfigureAwait(false);
        await db.DeleteAsync<SyncChangeRecord>((int)change.ChangeId).ConfigureAwait(false);
    }

    public async Task RequeueFailedAsync(string? collection = null, CancellationToken cancellationToken = default)
    {
        var db = await GetDbAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var failed = string.IsNullOrWhiteSpace(collection)
                ? await db.Table<SyncChangeRecord>().Where(record => record.IsFailed).ToListAsync().ConfigureAwait(false)
                : await db.Table<SyncChangeRecord>().Where(record => record.IsFailed && record.Collection == collection).ToListAsync().ConfigureAwait(false);

            foreach (var record in failed)
            {
                record.IsFailed = false;
                record.AttemptCount = 0;
                record.LastError = null;
                await db.UpdateAsync(record).ConfigureAwait(false);

                var document = await db.FindAsync<SyncDocumentRecord>(SyncDocumentRecord.MakeKey(record.Collection, record.EntityId)).ConfigureAwait(false);
                if (document is not null)
                {
                    document.SyncStateValue = (int)((ChangeOperation)record.Operation switch
                    {
                        ChangeOperation.Insert => SyncState.PendingCreate,
                        ChangeOperation.Delete => SyncState.PendingDelete,
                        _ => SyncState.PendingUpdate
                    });
                    await db.UpdateAsync(document).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> GetCursorAsync(string collection, CancellationToken cancellationToken = default)
    {
        var db = await GetDbAsync(cancellationToken).ConfigureAwait(false);
        var record = await db.FindAsync<SyncCollectionMetaRecord>(collection).ConfigureAwait(false);
        return record?.Cursor;
    }

    public async Task SetCursorAsync(string collection, string? cursor, CancellationToken cancellationToken = default)
    {
        var db = await GetDbAsync(cancellationToken).ConfigureAwait(false);
        await db.InsertOrReplaceAsync(new SyncCollectionMetaRecord
        {
            Collection = collection,
            Cursor = cursor,
            LastSyncAtUtc = DateTimeOffset.UtcNow.ToString("O")
        }).ConfigureAwait(false);
    }

    public async Task<int> GetPendingCountAsync(string? collection = null, CancellationToken cancellationToken = default)
    {
        var db = await GetDbAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(collection))
        {
            return await db.Table<SyncChangeRecord>().Where(record => !record.IsFailed).CountAsync().ConfigureAwait(false);
        }

        return await db.Table<SyncChangeRecord>()
            .Where(record => !record.IsFailed && record.Collection == collection)
            .CountAsync()
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetKnownCollectionsAsync(CancellationToken cancellationToken = default)
    {
        var db = await GetDbAsync(cancellationToken).ConfigureAwait(false);
        var fromDocs = await db.Table<SyncDocumentRecord>().ToListAsync().ConfigureAwait(false);
        var fromMeta = await db.Table<SyncCollectionMetaRecord>().ToListAsync().ConfigureAwait(false);
        return fromDocs.Select(record => record.Collection)
            .Concat(fromMeta.Select(record => record.Collection))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync().ConfigureAwait(false);
            _connection = null;
        }

        _gate.Dispose();
    }

    private async Task<SQLiteAsyncConnection> GetDbAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        return _connection!;
    }

    private static string ResolvePath(OfflineSyncOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.DatabasePath))
        {
            return options.DatabasePath;
        }

        try
        {
            return Path.Combine(FileSystem.AppDataDirectory, options.DatabaseFileName);
        }
        catch (Exception)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), options.DatabaseFileName);
        }
    }
}
