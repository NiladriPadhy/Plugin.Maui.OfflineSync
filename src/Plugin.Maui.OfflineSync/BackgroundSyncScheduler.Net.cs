#if !ANDROID && !IOS
namespace Plugin.Maui.OfflineSync;

/// <summary>
/// No-op scheduler used when building the net10.0 reference assembly.
/// </summary>
internal sealed class BackgroundSyncScheduler : IBackgroundSyncScheduler
{
    public void Schedule(TimeSpan interval)
    {
    }

    public void Cancel()
    {
    }
}
#endif
