namespace Plugin.Maui.OfflineSync.Conflicts;

internal static class ConflictResolverFactory
{
    public static IConflictResolver Create(ConflictStrategy strategy, IConflictResolver? custom)
    {
        return strategy switch
        {
            ConflictStrategy.ServerWins => new ServerWinsConflictResolver(),
            ConflictStrategy.ClientWins => new ClientWinsConflictResolver(),
            ConflictStrategy.Custom => custom ?? throw new InvalidOperationException(
                "ConflictStrategy.Custom requires OfflineSyncOptions.CustomConflictResolver."),
            _ => new LastWriteWinsConflictResolver()
        };
    }
}

internal sealed class LastWriteWinsConflictResolver : IConflictResolver
{
    public ConflictResolution Resolve(ConflictContext context)
    {
        return context.LocalUpdatedAtUtc > context.RemoteUpdatedAtUtc
            ? ConflictResolution.Local()
            : ConflictResolution.Remote();
    }
}

internal sealed class ServerWinsConflictResolver : IConflictResolver
{
    public ConflictResolution Resolve(ConflictContext context) => ConflictResolution.Remote();
}

internal sealed class ClientWinsConflictResolver : IConflictResolver
{
    public ConflictResolution Resolve(ConflictContext context) => ConflictResolution.Local();
}
