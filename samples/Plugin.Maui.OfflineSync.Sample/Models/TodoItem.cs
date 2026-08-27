using Plugin.Maui.OfflineSync;

namespace Plugin.Maui.OfflineSync.Sample.Models;

public sealed class TodoItem : SyncableEntity
{
    public string Title { get; set; } = string.Empty;

    public bool IsDone { get; set; }
}
