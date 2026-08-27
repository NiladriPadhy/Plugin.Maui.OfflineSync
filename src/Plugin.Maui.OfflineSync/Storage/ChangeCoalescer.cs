namespace Plugin.Maui.OfflineSync.Storage;

/// <summary>
/// Shared local-write rules used by both SQLite and in-memory stores.
/// </summary>
internal static class ChangeCoalescer
{
    public sealed class WritePlan
    {
        public bool RemoveDocument { get; init; }

        public SyncDocument? Document { get; init; }

        public PendingMutation? Mutation { get; init; }

        public bool DropExistingPending { get; init; }
    }

    public sealed class PendingMutation
    {
        public required ChangeOperation Operation { get; init; }

        public required string PayloadJson { get; init; }

        public required long BaseVersion { get; init; }

        public required DateTimeOffset CreatedAtUtc { get; init; }
    }

    public static WritePlan Plan(SyncDocument? existing, SyncDocument incoming, ChangeOperation operation)
    {
        if (operation == ChangeOperation.Delete)
        {
            if (existing is null)
            {
                return new WritePlan();
            }

            if (existing.SyncState == SyncState.PendingCreate)
            {
                return new WritePlan { RemoveDocument = true, DropExistingPending = true };
            }

            incoming.IsDeleted = true;
            incoming.SyncState = SyncState.PendingDelete;
            incoming.UpdatedAtUtc = DateTimeOffset.UtcNow;
            return new WritePlan
            {
                Document = incoming,
                DropExistingPending = true,
                Mutation = new PendingMutation
                {
                    Operation = ChangeOperation.Delete,
                    PayloadJson = incoming.PayloadJson,
                    BaseVersion = existing.Version,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                }
            };
        }

        if (existing is null || existing.SyncState == SyncState.PendingCreate)
        {
            incoming.SyncState = SyncState.PendingCreate;
            return new WritePlan
            {
                Document = incoming,
                DropExistingPending = existing is not null,
                Mutation = new PendingMutation
                {
                    Operation = ChangeOperation.Insert,
                    PayloadJson = incoming.PayloadJson,
                    BaseVersion = incoming.Version,
                    CreatedAtUtc = existing is null ? DateTimeOffset.UtcNow : existing.CreatedAtUtc
                }
            };
        }

        incoming.SyncState = SyncState.PendingUpdate;
        return new WritePlan
        {
            Document = incoming,
            DropExistingPending = true,
            Mutation = new PendingMutation
            {
                Operation = ChangeOperation.Update,
                PayloadJson = incoming.PayloadJson,
                BaseVersion = existing.Version,
                CreatedAtUtc = DateTimeOffset.UtcNow
            }
        };
    }
}
