namespace Plugin.Maui.OfflineSync;

/// <summary>
/// Constants the host app must declare when <see cref="OfflineSyncOptions.EnableBackgroundSync"/> is true.
/// </summary>
public static class OfflineSyncBackground
{
    /// <summary>
    /// iOS BGTaskScheduler identifier. Add it to BGTaskSchedulerPermittedIdentifiers in Info.plist.
    /// </summary>
    public const string iOSTaskIdentifier = "plugin.maui.offlinesync.refresh";
}
