using Plugin.Maui.OfflineSync.Conflicts;
using Plugin.Maui.OfflineSync.Networking;
using Plugin.Maui.OfflineSync.Remote;
using Plugin.Maui.OfflineSync.Storage;

namespace Plugin.Maui.OfflineSync.Tests;

internal static class EngineFixture
{
    public static (OfflineSyncEngine Engine, InMemorySyncStore Store, InMemoryRemoteSyncClient Remote, ManualNetworkMonitor Network) Create(
        ConflictStrategy strategy = ConflictStrategy.LastWriteWins)
    {
        var store = new InMemorySyncStore();
        var remote = new InMemoryRemoteSyncClient();
        var network = new ManualNetworkMonitor();
        var options = new OfflineSyncOptions
        {
            AutoSync = false,
            UseInMemoryStore = true,
            ConflictStrategy = strategy
        };

        var engine = new OfflineSyncEngine(
            store,
            remote,
            network,
            ConflictResolverFactory.Create(strategy, null),
            options);

        return (engine, store, remote, network);
    }
}
