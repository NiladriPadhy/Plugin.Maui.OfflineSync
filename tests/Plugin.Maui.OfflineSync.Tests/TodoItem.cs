namespace Plugin.Maui.OfflineSync.Tests;

public sealed class TodoItem : SyncableEntity
{
    public string Title { get; set; } = "";

    public bool IsDone { get; set; }
}
