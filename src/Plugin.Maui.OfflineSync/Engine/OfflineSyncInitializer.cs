using Microsoft.Maui;

namespace Plugin.Maui.OfflineSync;

internal sealed class OfflineSyncInitializer : IMauiInitializeService
{
    public void Initialize(IServiceProvider services)
    {
        var engine = services.GetRequiredService<IOfflineSyncEngine>();
        OfflineSync.SetDefault(engine);

        var options = services.GetRequiredService<OfflineSyncOptions>();
        if (options.AutoSync)
        {
            _ = StartAutoSyncSafeAsync(engine);
        }
    }

    static async Task StartAutoSyncSafeAsync(IOfflineSyncEngine engine)
    {
        try
        {
            await engine.StartAutoSyncAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // StatusChanged / SyncCompleted on the engine surface the failure to the host.
        }
    }
}
