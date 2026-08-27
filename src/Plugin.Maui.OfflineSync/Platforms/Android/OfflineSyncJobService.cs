using Android;
using Android.App;
using Android.App.Job;

[assembly: UsesPermission(Manifest.Permission.ReceiveBootCompleted)]

namespace Plugin.Maui.OfflineSync;

[Service(
    Name = "plugin.maui.offlinesync.OfflineSyncJobService",
    Permission = "android.permission.BIND_JOB_SERVICE",
    Exported = true)]
public sealed class OfflineSyncJobService : JobService
{
    public override bool OnStartJob(JobParameters? @params)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (OfflineSync.IsInitialized)
                {
                    await OfflineSync.Default.SyncAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                JobFinished(@params, false);
            }
        });

        return true;
    }

    public override bool OnStopJob(JobParameters? @params) => true;
}
