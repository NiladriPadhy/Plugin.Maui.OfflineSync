namespace Plugin.Maui.OfflineSync.Tests;

public sealed class SyncEngineTests
{
    [Fact]
    public async Task Sync_Pushes_Local_Changes_And_Marks_Synced()
    {
        var (engine, _, remote, _) = EngineFixture.Create();
        var todos = engine.GetCollection<TodoItem>("todos");
        var item = await todos.InsertAsync(new TodoItem { Title = "Ship it" });

        var result = await engine.SyncCollectionAsync("todos");

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Pushed);
        Assert.Equal(0, await engine.GetPendingCountAsync("todos"));
        Assert.Equal(SyncState.Synced, (await todos.GetAsync(item.Id))!.SyncState);
        Assert.NotNull(remote.Get("todos", item.Id));
    }

    [Fact]
    public async Task Offline_Sync_Skips_And_Keeps_Queue()
    {
        var (engine, _, _, network) = EngineFixture.Create();
        var todos = engine.GetCollection<TodoItem>("todos");
        await todos.InsertAsync(new TodoItem { Title = "Offline note" });
        network.SetOnline(false);

        var result = await engine.SyncCollectionAsync("todos");

        Assert.True(result.Skipped);
        Assert.Equal(1, await engine.GetPendingCountAsync("todos"));
        Assert.Equal(SyncStatus.Offline, engine.Status);
    }

    [Fact]
    public async Task Pull_Imports_Remote_Documents()
    {
        var (engine, _, remote, _) = EngineFixture.Create();
        engine.GetCollection<TodoItem>("todos");
        remote.Seed("todos", new RemoteDocument
        {
            Id = "remote-1",
            Version = 3,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            PayloadJson = """{"id":"remote-1","title":"From server","isDone":false,"version":3}"""
        });

        var result = await engine.SyncCollectionAsync("todos");
        var pulled = await engine.GetCollection<TodoItem>("todos").GetAsync("remote-1");

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Pulled);
        Assert.NotNull(pulled);
        Assert.Equal("From server", pulled!.Title);
        Assert.Equal(SyncState.Synced, pulled.SyncState);
    }

    [Fact]
    public async Task LastWriteWins_Keeps_Newer_Local_Change()
    {
        var (engine, _, remote, _) = EngineFixture.Create();
        var todos = engine.GetCollection<TodoItem>("todos");
        var item = await todos.InsertAsync(new TodoItem { Title = "Local" });
        await engine.SyncCollectionAsync("todos");

        remote.Seed("todos", new RemoteDocument
        {
            Id = item.Id,
            Version = 2,
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            PayloadJson = """{"id":"ignored","title":"Older server","isDone":false,"version":2}"""
        });

        item.Title = "Newer local";
        await todos.UpdateAsync(item);
        var result = await engine.SyncCollectionAsync("todos");

        Assert.True(result.Succeeded);
        Assert.True(result.Conflicts >= 1);
        Assert.Equal("Newer local", (await todos.GetAsync(item.Id))!.Title);
        Assert.Equal("Newer local", System.Text.Json.JsonDocument.Parse(remote.Get("todos", item.Id)!.PayloadJson).RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ServerWins_Overwrites_Local_On_Conflict()
    {
        var (engine, _, remote, _) = EngineFixture.Create(ConflictStrategy.ServerWins);
        var todos = engine.GetCollection<TodoItem>("todos");
        var item = await todos.InsertAsync(new TodoItem { Title = "Local" });
        await engine.SyncCollectionAsync("todos");

        remote.Seed("todos", new RemoteDocument
        {
            Id = item.Id,
            Version = 9,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            PayloadJson = """{"id":"x","title":"Server wins","isDone":true,"version":9}"""
        });

        item.Title = "Client edit";
        await todos.UpdateAsync(item);
        await engine.SyncCollectionAsync("todos");

        var loaded = await todos.GetAsync(item.Id);
        Assert.Equal("Server wins", loaded!.Title);
        Assert.Equal(SyncState.Synced, loaded.SyncState);
        Assert.Equal(0, await engine.GetPendingCountAsync("todos"));
    }

    [Fact]
    public async Task Failed_Changes_Can_Be_Requeued()
    {
        var (engine, _, _, _) = EngineFixture.Create();
        var todos = engine.GetCollection<TodoItem>("todos");
        var item = await todos.InsertAsync(new TodoItem { Title = "Retry me" });

        var rejecting = new RejectingRemote();
        await using var isolated = new OfflineSyncEngine(
            new Storage.InMemorySyncStore(),
            rejecting,
            new Networking.ManualNetworkMonitor(),
            new Conflicts.LastWriteWinsConflictResolver(),
            new OfflineSyncOptions { AutoSync = false, MaxRetryAttempts = 1 });

        var isolatedTodos = isolated.GetCollection<TodoItem>("todos");
        await isolatedTodos.InsertAsync(new TodoItem { Id = item.Id, Title = "Retry me" });
        var result = await isolated.SyncCollectionAsync("todos");

        Assert.False(result.Succeeded);
        Assert.True(result.Failed >= 1);

        await isolated.RequeueFailedAsync("todos");
        Assert.Equal(1, await isolated.GetPendingCountAsync("todos"));
    }

    private sealed class RejectingRemote : IRemoteSyncClient
    {
        public Task<PullResponse> PullAsync(string collection, string? cursor, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PullResponse());

        public Task<PushResponse> PushAsync(string collection, IReadOnlyList<PushChange> changes, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PushResponse
            {
                Rejected = changes.Select(change => new RejectedChange { Id = change.Id, Error = "nope" }).ToList()
            });
    }
}
