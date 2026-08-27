using Android.App.Job;
using Android.Content;
using Application = Android.App.Application;

namespace Plugin.Maui.OfflineSync;

internal sealed class BackgroundSyncScheduler : IBackgroundSyncScheduler
{
    internal const int JobId = 718821;

    public void Schedule(TimeSpan interval)
    {
        var context = Application.Context;
        if (context.GetSystemService(Context.JobSchedulerService) is not JobScheduler scheduler)
        {
            return;
        }

        var component = new ComponentName(context, Java.Lang.Class.FromType(typeof(OfflineSyncJobService)));
        var periodMs = Math.Max((long)interval.TotalMilliseconds, 15 * 60 * 1000L);
        var builder = new JobInfo.Builder(JobId, component);
        builder.SetRequiredNetworkType(NetworkType.Any);
        builder.SetPeriodic(periodMs);
        builder.SetPersisted(true);
        var job = builder.Build();
        if (job is not null)
        {
            scheduler.Schedule(job);
        }
    }

    public void Cancel()
    {
        var context = Application.Context;
        if (context.GetSystemService(Context.JobSchedulerService) is JobScheduler scheduler)
        {
            scheduler.Cancel(JobId);
        }
    }
}
