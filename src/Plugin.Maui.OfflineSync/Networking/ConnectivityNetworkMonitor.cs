namespace Plugin.Maui.OfflineSync.Networking;

/// <summary>
/// Default monitor backed by MAUI <see cref="Microsoft.Maui.Networking.Connectivity"/>.
/// </summary>
public sealed class ConnectivityNetworkMonitor : INetworkMonitor, IDisposable
{
    public ConnectivityNetworkMonitor()
    {
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    public bool IsOnline
    {
        get
        {
            try
            {
                return Connectivity.Current.NetworkAccess is NetworkAccess.Internet or NetworkAccess.ConstrainedInternet;
            }
            catch
            {
                return true;
            }
        }
    }

    public event EventHandler<bool>? ConnectivityChanged;

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e) =>
        ConnectivityChanged?.Invoke(this, IsOnline);

    public void Dispose() => Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
}

/// <summary>
/// Deterministic monitor used by tests and the sample app's offline toggle.
/// </summary>
public sealed class ManualNetworkMonitor : INetworkMonitor
{
    private bool _isOnline = true;

    public bool IsOnline => _isOnline;

    public event EventHandler<bool>? ConnectivityChanged;

    public void SetOnline(bool online)
    {
        if (_isOnline == online)
        {
            return;
        }

        _isOnline = online;
        ConnectivityChanged?.Invoke(this, online);
    }
}
