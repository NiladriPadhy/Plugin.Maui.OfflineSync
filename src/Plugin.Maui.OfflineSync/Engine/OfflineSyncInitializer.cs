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
            _ = engine.StartAutoSyncAsync();
        }
    }
}
