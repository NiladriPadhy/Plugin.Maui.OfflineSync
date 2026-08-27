using Microsoft.Extensions.Logging;
using Plugin.Maui.OfflineSync;
using Plugin.Maui.OfflineSync.Networking;
using Plugin.Maui.OfflineSync.Remote;
using Plugin.Maui.OfflineSync.Sample.ViewModels;

namespace Plugin.Maui.OfflineSync.Sample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        var network = new ManualNetworkMonitor();
        var remote = new InMemoryRemoteSyncClient();

        builder.Services.AddSingleton<INetworkMonitor>(network);

        builder
            .UseMauiApp<App>()
            .UseOfflineSync(options =>
            {
                options.AutoSync = false;
                options.RemoteClient = remote;
                options.ConflictStrategy = ConflictStrategy.LastWriteWins;
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton(remote);
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
