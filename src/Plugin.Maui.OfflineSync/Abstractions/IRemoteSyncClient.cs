namespace Plugin.Maui.OfflineSync;

/// <summary>
/// Transport used to exchange change batches with a remote source of truth.
/// </summary>
public interface IRemoteSyncClient
{
    Task<PullResponse> PullAsync(string collection, string? cursor, CancellationToken cancellationToken = default);

    Task<PushResponse> PushAsync(string collection, IReadOnlyList<PushChange> changes, CancellationToken cancellationToken = default);
}
