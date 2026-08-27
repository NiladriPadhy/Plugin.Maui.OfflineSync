using Plugin.Maui.OfflineSync.Conflicts;

namespace Plugin.Maui.OfflineSync.Tests;

public sealed class ConflictResolverTests
{
    [Fact]
    public void LastWriteWins_Prefers_Newer_Timestamp()
    {
        var resolver = new LastWriteWinsConflictResolver();
        var localNewer = resolver.Resolve(Context(localOffsetMinutes: 0, remoteOffsetMinutes: -10));
        var remoteNewer = resolver.Resolve(Context(localOffsetMinutes: -10, remoteOffsetMinutes: 0));

        Assert.Equal(ConflictWinner.Local, localNewer.Winner);
        Assert.Equal(ConflictWinner.Remote, remoteNewer.Winner);
    }

    [Fact]
    public void ServerWins_Always_Remote()
    {
        var resolver = new ServerWinsConflictResolver();
        var result = resolver.Resolve(Context(localOffsetMinutes: 0, remoteOffsetMinutes: -30));
        Assert.Equal(ConflictWinner.Remote, result.Winner);
    }

    [Fact]
    public void ClientWins_Always_Local()
    {
        var resolver = new ClientWinsConflictResolver();
        var result = resolver.Resolve(Context(localOffsetMinutes: -30, remoteOffsetMinutes: 0));
        Assert.Equal(ConflictWinner.Local, result.Winner);
    }

    private static ConflictContext Context(int localOffsetMinutes, int remoteOffsetMinutes) =>
        new()
        {
            Collection = "todos",
            EntityId = "1",
            LocalJson = "{}",
            RemoteJson = "{}",
            LocalUpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(localOffsetMinutes),
            RemoteUpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(remoteOffsetMinutes)
        };
}
