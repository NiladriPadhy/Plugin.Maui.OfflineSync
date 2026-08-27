namespace Plugin.Maui.OfflineSync;

/// <summary>
/// Reports connectivity so the engine can queue locally and sync when online.
/// </summary>
public interface INetworkMonitor
{
    bool IsOnline { get; }

    event EventHandler<bool>? ConnectivityChanged;
}
