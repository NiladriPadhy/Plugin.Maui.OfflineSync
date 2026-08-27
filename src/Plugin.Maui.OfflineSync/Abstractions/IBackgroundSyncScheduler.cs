namespace Plugin.Maui.OfflineSync;

/// <summary>
/// Platform scheduler for OS-managed background synchronization.
/// </summary>
public interface IBackgroundSyncScheduler
{
    void Schedule(TimeSpan interval);

    void Cancel();
}
