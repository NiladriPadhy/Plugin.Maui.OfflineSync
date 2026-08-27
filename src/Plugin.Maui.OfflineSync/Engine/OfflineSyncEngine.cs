namespace Plugin.Maui.OfflineSync;

/// <summary>
/// Default offline-first synchronization engine.
/// </summary>
public sealed class OfflineSyncEngine : IOfflineSyncEngine
{
    private readonly ISyncStore _store;
    private readonly IRemoteSyncClient _remote;
    private readonly INetworkMonitor _network;
    private readonly IConflictResolver _resolver;
    private readonly OfflineSyncOptions _options;
    private readonly IBackgroundSyncScheduler _background;
    private readonly ConcurrentDictionary<string, object> _collections = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly HashSet<string> _knownCollections = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _autoCts;
    private Task? _autoLoop;
    private bool _initialized;
    private SyncStatus _status = SyncStatus.Idle;

    public OfflineSyncEngine(
        ISyncStore store,
        IRemoteSyncClient remote,
        INetworkMonitor network,
        IConflictResolver resolver,
        OfflineSyncOptions options,
        IBackgroundSyncScheduler? background = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _remote = remote ?? throw new ArgumentNullException(nameof(remote));
        _network = network ?? throw new ArgumentNullException(nameof(network));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _background = background ?? new BackgroundSyncScheduler();
    }

    public SyncStatus Status => _status;

    public bool IsOnline => _network.IsOnline;

    public event EventHandler<SyncStatusChangedEventArgs>? StatusChanged;

    public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;

    public event EventHandler<ConflictDetectedEventArgs>? ConflictDetected;

    public event EventHandler<CollectionChangedEventArgs>? CollectionChanged;

    public ISyncCollection<T> GetCollection<T>(string? name = null) where T : SyncableEntity, new()
    {
        var collection = string.IsNullOrWhiteSpace(name) ? typeof(T).Name : name;
        _knownCollections.Add(collection);
        return (ISyncCollection<T>)_collections.GetOrAdd(
            collection,
            key => new SyncCollection<T>(key, this, _store, _options));
    }

    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var collections = _knownCollections
            .Concat(await _store.GetKnownCollectionsAsync(cancellationToken).ConfigureAwait(false))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (collections.Count == 0)
        {
            return SyncResult.Skip("No collections have been registered yet.");
        }

        var combined = new SyncResult { Succeeded = true };
        var errors = new List<string>();
        foreach (var collection in collections)
        {
            var result = await SyncCollectionAsync(collection, cancellationToken).ConfigureAwait(false);
            combined.Pushed += result.Pushed;
            combined.Pulled += result.Pulled;
            combined.Conflicts += result.Conflicts;
            combined.Failed += result.Failed;
            if (!result.Succeeded)
            {
                combined = new SyncResult
                {
                    Succeeded = false,
                    Pushed = combined.Pushed,
                    Pulled = combined.Pulled,
                    Conflicts = combined.Conflicts,
                    Failed = combined.Failed,
                    Message = result.Message,
                    Errors = errors
                };
                errors.AddRange(result.Errors);
            }
        }

        return combined.Succeeded
            ? SyncResult.Ok(combined.Pushed, combined.Pulled, combined.Conflicts)
            : SyncResult.Fail(combined.Message ?? "One or more collections failed to sync.", errors);
    }

    public async Task<SyncResult> SyncCollectionAsync(string collection, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        _knownCollections.Add(collection);

        if (_options.LocalOnly)
        {
            return SyncResult.Skip("LocalOnly is enabled.");
        }

        if (!_network.IsOnline)
        {
            SetStatus(SyncStatus.Offline);
            var offline = SyncResult.Skip("Device is offline. Changes remain queued.");
            SyncCompleted?.Invoke(this, new SyncCompletedEventArgs { Result = offline, Collection = collection });
            return offline;
        }

        if (_remote is NullRemoteSyncClient)
        {
            return SyncResult.Fail(NullRemoteSyncClient.NotConfiguredMessage);
        }

        await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var previous = _status;
        SetStatus(SyncStatus.Syncing);
        var errors = new List<string>();
        var pushed = 0;
        var pulled = 0;
        var conflicts = 0;
        var failed = 0;

        try
        {
            var push = await PushAsync(collection, cancellationToken).ConfigureAwait(false);
            pushed = push.Pushed;
            conflicts += push.Conflicts;
            failed += push.Failed;
            errors.AddRange(push.Errors);

            var pull = await PullAsync(collection, cancellationToken).ConfigureAwait(false);
            pulled = pull.Pulled;
            conflicts += pull.Conflicts;
            errors.AddRange(pull.Errors);

            var succeeded = failed == 0 && errors.Count == 0;
            var result = succeeded
                ? SyncResult.Ok(pushed, pulled, conflicts)
                : new SyncResult
                {
                    Succeeded = false,
                    Pushed = pushed,
                    Pulled = pulled,
                    Conflicts = conflicts,
                    Failed = failed,
                    Message = errors.FirstOrDefault() ?? "Sync completed with errors.",
                    Errors = errors
                };

            SetStatus(succeeded ? SyncStatus.Idle : SyncStatus.Failed);
            SyncCompleted?.Invoke(this, new SyncCompletedEventArgs { Result = result, Collection = collection });
            return result;
        }
        catch (OperationCanceledException)
        {
            SetStatus(previous);
            throw;
        }
        catch (Exception ex)
        {
            var result = SyncResult.Fail(ex.Message);
            SetStatus(SyncStatus.Failed);
            SyncCompleted?.Invoke(this, new SyncCompletedEventArgs { Result = result, Collection = collection });
            return result;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task StartAutoSyncAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await StopAutoSyncAsync().ConfigureAwait(false);

        _autoCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _network.ConnectivityChanged += OnConnectivityChanged;

        if (_options.EnableBackgroundSync)
        {
            _background.Schedule(_options.AutoSyncInterval);
        }

        if (_options.AutoSyncInterval > TimeSpan.Zero)
        {
            _autoLoop = RunAutoSyncLoopAsync(_autoCts.Token);
        }

        if (_network.IsOnline)
        {
            _ = SafeSyncAsync(_autoCts.Token);
        }
        else
        {
            SetStatus(SyncStatus.Offline);
        }
    }

    public Task StopAutoSyncAsync()
    {
        _network.ConnectivityChanged -= OnConnectivityChanged;
        _background.Cancel();

        if (_autoCts is not null)
        {
            _autoCts.Cancel();
            _autoCts.Dispose();
            _autoCts = null;
        }

        _autoLoop = null;
        return Task.CompletedTask;
    }

    public async Task<int> GetPendingCountAsync(string? collection = null, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return await _store.GetPendingCountAsync(collection, cancellationToken).ConfigureAwait(false);
    }

    public async Task RequeueFailedAsync(string? collection = null, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await _store.RequeueFailedAsync(collection, cancellationToken).ConfigureAwait(false);
    }

    internal async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _store.InitializeAsync(cancellationToken).ConfigureAwait(false);
        _initialized = true;
    }

    internal void NotifyCollectionChanged(string collection, string entityId, ChangeOperation operation, bool fromRemote)
    {
        _knownCollections.Add(collection);
        CollectionChanged?.Invoke(this, new CollectionChangedEventArgs
        {
            Collection = collection,
            EntityId = entityId,
            Operation = operation,
            FromRemote = fromRemote
        });
    }

    private async Task<SyncResult> PushAsync(string collection, CancellationToken cancellationToken)
    {
        var pushed = 0;
        var conflicts = 0;
        var failed = 0;
        var errors = new List<string>();

        while (true)
        {
            var pending = await _store.GetPendingChangesAsync(collection, _options.PushBatchSize, cancellationToken).ConfigureAwait(false);
            if (pending.Count == 0)
            {
                break;
            }

            PushResponse response;
            try
            {
                response = await _remote.PushAsync(collection, pending.Select(ToPushChange).ToList(), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                foreach (var change in pending)
                {
                    var permanentlyFailed = change.AttemptCount + 1 >= _options.MaxRetryAttempts;
                    await _store.MarkChangeFailedAsync(change, ex.Message, permanentlyFailed, cancellationToken).ConfigureAwait(false);
                    if (permanentlyFailed)
                    {
                        failed++;
                    }
                }

                errors.Add(ex.Message);
                break;
            }

            foreach (var accepted in response.Accepted)
            {
                var change = pending.FirstOrDefault(item => item.EntityId == accepted.Id);
                if (change is null)
                {
                    continue;
                }

                await _store.MarkChangeSyncedAsync(change, accepted.Version, accepted.UpdatedAtUtc, cancellationToken: cancellationToken).ConfigureAwait(false);
                pushed++;
                NotifyCollectionChanged(collection, accepted.Id, change.Operation, fromRemote: false);
            }

            foreach (var remoteConflict in response.Conflicts)
            {
                var change = pending.FirstOrDefault(item => item.EntityId == remoteConflict.Id);
                if (change is null)
                {
                    continue;
                }

                conflicts++;
                await ResolvePushConflictAsync(collection, change, remoteConflict, cancellationToken).ConfigureAwait(false);
            }

            foreach (var rejected in response.Rejected)
            {
                var change = pending.FirstOrDefault(item => item.EntityId == rejected.Id);
                if (change is null)
                {
                    continue;
                }

                var permanentlyFailed = change.AttemptCount + 1 >= _options.MaxRetryAttempts;
                await _store.MarkChangeFailedAsync(change, rejected.Error ?? "Remote rejected the change.", permanentlyFailed, cancellationToken).ConfigureAwait(false);
                if (permanentlyFailed)
                {
                    failed++;
                }

                errors.Add(rejected.Error ?? $"Change {rejected.Id} was rejected.");
            }

            if (response.Accepted.Count == 0 && response.Conflicts.Count == 0)
            {
                break;
            }
        }

        return new SyncResult
        {
            Succeeded = failed == 0 && errors.Count == 0,
            Pushed = pushed,
            Conflicts = conflicts,
            Failed = failed,
            Errors = errors
        };
    }

    private async Task ResolvePushConflictAsync(string collection, PendingChange change, RemoteConflict remoteConflict, CancellationToken cancellationToken)
    {
        var resolution = _resolver.Resolve(new ConflictContext
        {
            Collection = collection,
            EntityId = change.EntityId,
            LocalJson = change.PayloadJson,
            RemoteJson = remoteConflict.ServerPayloadJson,
            LocalUpdatedAtUtc = change.CreatedAtUtc,
            RemoteUpdatedAtUtc = remoteConflict.ServerUpdatedAtUtc,
            LocalVersion = change.BaseVersion,
            RemoteVersion = remoteConflict.ServerVersion,
            LocalIsDeleted = change.Operation == ChangeOperation.Delete,
            RemoteIsDeleted = remoteConflict.ServerIsDeleted
        });

        ConflictDetected?.Invoke(this, new ConflictDetectedEventArgs
        {
            Collection = collection,
            EntityId = change.EntityId,
            Winner = resolution.Winner
        });

        if (resolution.Winner == ConflictWinner.Remote)
        {
            await _store.ApplyRemoteAsync(new SyncDocument
            {
                Collection = collection,
                Id = change.EntityId,
                PayloadJson = remoteConflict.ServerPayloadJson,
                Version = remoteConflict.ServerVersion,
                UpdatedAtUtc = remoteConflict.ServerUpdatedAtUtc,
                IsDeleted = remoteConflict.ServerIsDeleted,
                SyncState = SyncState.Synced
            }, cancellationToken).ConfigureAwait(false);
            NotifyCollectionChanged(collection, change.EntityId, ChangeOperation.Update, fromRemote: true);
            return;
        }

        var payload = resolution.Winner == ConflictWinner.Merged && !string.IsNullOrWhiteSpace(resolution.MergedJson)
            ? resolution.MergedJson
            : change.PayloadJson;

        await _store.ApplyRemoteAsync(new SyncDocument
        {
            Collection = collection,
            Id = change.EntityId,
            PayloadJson = payload,
            Version = remoteConflict.ServerVersion,
            UpdatedAtUtc = remoteConflict.ServerUpdatedAtUtc,
            IsDeleted = false,
            SyncState = SyncState.Synced
        }, cancellationToken).ConfigureAwait(false);

        await _store.UpsertLocalAsync(new SyncDocument
        {
            Collection = collection,
            Id = change.EntityId,
            PayloadJson = payload,
            Version = remoteConflict.ServerVersion,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            IsDeleted = change.Operation == ChangeOperation.Delete,
            SyncState = change.Operation == ChangeOperation.Delete ? SyncState.PendingDelete : SyncState.PendingUpdate
        }, change.Operation == ChangeOperation.Delete ? ChangeOperation.Delete : ChangeOperation.Update, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SyncResult> PullAsync(string collection, CancellationToken cancellationToken)
    {
        var cursor = await _store.GetCursorAsync(collection, cancellationToken).ConfigureAwait(false);
        var pull = await _remote.PullAsync(collection, cursor, cancellationToken).ConfigureAwait(false);
        var pulled = 0;
        var conflicts = 0;

        foreach (var item in pull.Items)
        {
            var local = await _store.GetAsync(collection, item.Id, cancellationToken).ConfigureAwait(false);
            if (local is null || !IsDirty(local.SyncState))
            {
                await _store.ApplyRemoteAsync(new SyncDocument
                {
                    Collection = collection,
                    Id = item.Id,
                    PayloadJson = item.PayloadJson,
                    Version = item.Version,
                    CreatedAtUtc = item.CreatedAtUtc == default ? item.UpdatedAtUtc : item.CreatedAtUtc,
                    UpdatedAtUtc = item.UpdatedAtUtc,
                    IsDeleted = item.IsDeleted,
                    SyncState = SyncState.Synced
                }, cancellationToken).ConfigureAwait(false);
                pulled++;
                NotifyCollectionChanged(collection, item.Id, item.IsDeleted ? ChangeOperation.Delete : ChangeOperation.Update, fromRemote: true);
                continue;
            }

            conflicts++;
            var resolution = _resolver.Resolve(new ConflictContext
            {
                Collection = collection,
                EntityId = item.Id,
                LocalJson = local.PayloadJson,
                RemoteJson = item.PayloadJson,
                LocalUpdatedAtUtc = local.UpdatedAtUtc,
                RemoteUpdatedAtUtc = item.UpdatedAtUtc,
                LocalVersion = local.Version,
                RemoteVersion = item.Version,
                LocalIsDeleted = local.IsDeleted,
                RemoteIsDeleted = item.IsDeleted
            });

            ConflictDetected?.Invoke(this, new ConflictDetectedEventArgs
            {
                Collection = collection,
                EntityId = item.Id,
                Winner = resolution.Winner
            });

            if (resolution.Winner == ConflictWinner.Remote)
            {
                await _store.ApplyRemoteAsync(new SyncDocument
                {
                    Collection = collection,
                    Id = item.Id,
                    PayloadJson = item.PayloadJson,
                    Version = item.Version,
                    UpdatedAtUtc = item.UpdatedAtUtc,
                    IsDeleted = item.IsDeleted,
                    SyncState = SyncState.Synced
                }, cancellationToken).ConfigureAwait(false);
                NotifyCollectionChanged(collection, item.Id, ChangeOperation.Update, fromRemote: true);
            }
        }

        if (pull.Cursor is not null)
        {
            await _store.SetCursorAsync(collection, pull.Cursor, cancellationToken).ConfigureAwait(false);
        }

        return new SyncResult { Succeeded = true, Pulled = pulled, Conflicts = conflicts };
    }

    private async Task RunAutoSyncLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_options.AutoSyncInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await SafeSyncAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // shut down
        }
    }

    private async Task SafeSyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SyncAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch
        {
            SetStatus(SyncStatus.Failed);
        }
    }

    private void OnConnectivityChanged(object? sender, bool online)
    {
        if (!online)
        {
            SetStatus(SyncStatus.Offline);
            return;
        }

        if (_options.SyncOnNetworkRestored)
        {
            _ = SafeSyncAsync(_autoCts?.Token ?? CancellationToken.None);
        }
        else
        {
            SetStatus(SyncStatus.Idle);
        }
    }

    private void SetStatus(SyncStatus status)
    {
        if (_status == status)
        {
            return;
        }

        var previous = _status;
        _status = status;
        StatusChanged?.Invoke(this, new SyncStatusChangedEventArgs { Status = status, Previous = previous });
    }

    private static bool IsDirty(SyncState state) =>
        state is SyncState.PendingCreate or SyncState.PendingUpdate or SyncState.PendingDelete;

    private static PushChange ToPushChange(PendingChange change) =>
        new()
        {
            Id = change.EntityId,
            Operation = change.Operation,
            BaseVersion = change.BaseVersion,
            UpdatedAtUtc = change.CreatedAtUtc,
            PayloadJson = change.PayloadJson,
            Force = change.Force
        };

    public async ValueTask DisposeAsync()
    {
        await StopAutoSyncAsync().ConfigureAwait(false);
        await _store.DisposeAsync().ConfigureAwait(false);
        _syncLock.Dispose();
    }
}
