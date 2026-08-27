namespace Plugin.Maui.OfflineSync.Remote;

internal sealed class NullRemoteSyncClient : IRemoteSyncClient
{
    public const string NotConfiguredMessage =
        "No remote sync client is configured. Set RemoteClient, RemoteBaseAddress, or LocalOnly = true.";

    public Task<PullResponse> PullAsync(string collection, string? cursor, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PullResponse());

    public Task<PushResponse> PushAsync(string collection, IReadOnlyList<PushChange> changes, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PushResponse
        {
            Rejected = changes.Select(change => new RejectedChange
            {
                Id = change.Id,
                Error = NotConfiguredMessage
            }).ToList()
        });
    }
}
