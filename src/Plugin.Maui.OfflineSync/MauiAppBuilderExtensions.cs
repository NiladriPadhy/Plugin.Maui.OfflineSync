using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.LifecycleEvents;

namespace Plugin.Maui.OfflineSync;

/// <summary>
/// MAUI host registration for the offline-first sync engine.
/// </summary>
public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Registers OfflineSync services, the default engine, and optional lifecycle hooks.
    /// </summary>
    public static MauiAppBuilder UseOfflineSync(this MauiAppBuilder builder, Action<OfflineSyncOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new OfflineSyncOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.TryAddSingleton<IConflictResolver>(sp =>
        {
            var opts = sp.GetRequiredService<OfflineSyncOptions>();
            return ConflictResolverFactory.Create(opts.ConflictStrategy, opts.CustomConflictResolver);
        });
        builder.Services.TryAddSingleton<INetworkMonitor, ConnectivityNetworkMonitor>();
        builder.Services.TryAddSingleton<IBackgroundSyncScheduler, BackgroundSyncScheduler>();
        builder.Services.TryAddSingleton<IRemoteSyncClient>(sp => CreateRemoteClient(sp.GetRequiredService<OfflineSyncOptions>()));
        builder.Services.TryAddSingleton<ISyncStore>(sp =>
        {
            var opts = sp.GetRequiredService<OfflineSyncOptions>();
            return opts.UseInMemoryStore ? new InMemorySyncStore() : new SqliteSyncStore(opts);
        });
        builder.Services.TryAddSingleton<IOfflineSyncEngine, OfflineSyncEngine>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IMauiInitializeService, OfflineSyncInitializer>());

        if (options.SyncOnResume)
        {
            builder.ConfigureLifecycleEvents(events =>
            {
#if ANDROID
                events.AddAndroid(android => android.OnResume(_ => ResumeSync()));
#elif IOS
                events.AddiOS(ios => ios.OnActivated(_ => ResumeSync()));
#endif
            });
        }

        return builder;
    }

    private static IRemoteSyncClient CreateRemoteClient(OfflineSyncOptions options)
    {
        if (options.RemoteClient is not null)
        {
            return options.RemoteClient;
        }

        if (options.RemoteBaseAddress is not null)
        {
            return new HttpRemoteSyncClient(new HttpRemoteOptions
            {
                BaseAddress = options.RemoteBaseAddress,
                AccessToken = options.RemoteAccessToken,
                AccessTokenProvider = options.RemoteAccessTokenProvider
            });
        }

        return new NullRemoteSyncClient();
    }

    private static void ResumeSync()
    {
        if (!OfflineSync.IsInitialized)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await OfflineSync.Default.SyncAsync().ConfigureAwait(false);
            }
            catch
            {
                // Resume sync is best-effort; failures surface through SyncCompleted.
            }
        });
    }
}
