using BackgroundTasks;
using Foundation;

namespace Plugin.Maui.OfflineSync;

internal sealed class BackgroundSyncScheduler : IBackgroundSyncScheduler
{
    public const string TaskIdentifier = "plugin.maui.offlinesync.refresh";

    private static bool _registered;

    public BackgroundSyncScheduler()
    {
        RegisterIfNeeded();
    }

    public void Schedule(TimeSpan interval)
    {
        RegisterIfNeeded();

        var request = new BGAppRefreshTaskRequest(TaskIdentifier)
        {
            EarliestBeginDate = NSDate.FromTimeIntervalSinceNow(Math.Max(interval.TotalSeconds, 15 * 60))
        };

        BGTaskScheduler.Shared.Submit(request, out _);
    }

    public void Cancel() => BGTaskScheduler.Shared.Cancel(TaskIdentifier);

    private static void RegisterIfNeeded()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        BGTaskScheduler.Shared.Register(TaskIdentifier, null, task =>
        {
            var refresh = (BGAppRefreshTask)task;
            var cts = new CancellationTokenSource();
            refresh.ExpirationHandler = () => cts.Cancel();

            _ = Task.Run(async () =>
            {
                var completed = false;
                try
                {
                    if (OfflineSync.IsInitialized)
                    {
                        var result = await OfflineSync.Default.SyncAsync(cts.Token).ConfigureAwait(false);
                        completed = result.Succeeded || result.Skipped;
                    }

                    new BackgroundSyncScheduler().Schedule(TimeSpan.FromMinutes(15));
                    refresh.SetTaskCompleted(completed);
                }
                catch
                {
                    refresh.SetTaskCompleted(false);
                }
            });
        });
    }
}
