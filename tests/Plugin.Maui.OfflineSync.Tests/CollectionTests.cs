using Plugin.Maui.OfflineSync.Storage;

namespace Plugin.Maui.OfflineSync.Tests;

public sealed class CollectionTests
{
    [Fact]
    public async Task Insert_Persists_Locally_And_Queues_Change()
    {
        var (engine, store, _, _) = EngineFixture.Create();
        var todos = engine.GetCollection<TodoItem>("todos");

        var created = await todos.InsertAsync(new TodoItem { Title = "Buy milk" });

        Assert.False(string.IsNullOrWhiteSpace(created.Id));
        var loaded = await todos.GetAsync(created.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Buy milk", loaded!.Title);
        Assert.Equal(1, await store.GetPendingCountAsync("todos"));
    }

    [Fact]
    public async Task Update_Of_Unsynced_Insert_Coalesces_To_Single_Change()
    {
        var (engine, store, _, _) = EngineFixture.Create();
        var todos = engine.GetCollection<TodoItem>("todos");
        var item = await todos.InsertAsync(new TodoItem { Title = "Draft" });

        item.Title = "Final";
        await todos.UpdateAsync(item);

        Assert.Equal(1, await store.GetPendingCountAsync("todos"));
        var pending = await store.GetPendingChangesAsync("todos");
        Assert.Equal(ChangeOperation.Insert, pending[0].Operation);
        Assert.Contains("Final", pending[0].PayloadJson);
    }

    [Fact]
    public async Task Delete_Of_Unsynced_Insert_Removes_Local_Document()
    {
        var (engine, store, _, _) = EngineFixture.Create();
        var todos = engine.GetCollection<TodoItem>("todos");
        var item = await todos.InsertAsync(new TodoItem { Title = "Temp" });

        await todos.DeleteAsync(item.Id);

        Assert.Null(await todos.GetAsync(item.Id));
        Assert.Equal(0, await store.GetPendingCountAsync("todos"));
    }

    [Fact]
    public async Task Query_Filters_Deleted_Items()
    {
        var (engine, _, remote, _) = EngineFixture.Create();
        var todos = engine.GetCollection<TodoItem>("todos");
        var keep = await todos.InsertAsync(new TodoItem { Title = "Keep" });
        var drop = await todos.InsertAsync(new TodoItem { Title = "Drop" });
        await engine.SyncCollectionAsync("todos");

        await todos.DeleteAsync(drop.Id);

        var remaining = await todos.GetAllAsync();
        Assert.Single(remaining);
        Assert.Equal(keep.Id, remaining[0].Id);
        Assert.NotNull(remote.Get("todos", drop.Id));
    }
}
