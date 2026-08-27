using SQLite;

namespace Plugin.Maui.OfflineSync.Storage;

internal sealed class SyncDocumentRecord
{
    [PrimaryKey]
    public string Key { get; set; } = "";

    [Indexed]
    public string Collection { get; set; } = "";

    [Indexed]
    public string EntityId { get; set; } = "";

    public string PayloadJson { get; set; } = "{}";

    public long Version { get; set; }

    public string CreatedAtUtc { get; set; } = "";

    public string UpdatedAtUtc { get; set; } = "";

    public bool IsDeleted { get; set; }

    public int SyncStateValue { get; set; }

    public static string MakeKey(string collection, string id) => $"{collection}:{id}";

    public SyncDocument ToDocument() =>
        new()
        {
            Collection = Collection,
            Id = EntityId,
            PayloadJson = PayloadJson,
            Version = Version,
            CreatedAtUtc = DateTimeOffset.Parse(CreatedAtUtc),
            UpdatedAtUtc = DateTimeOffset.Parse(UpdatedAtUtc),
            IsDeleted = IsDeleted,
            SyncState = (SyncState)SyncStateValue
        };

    public static SyncDocumentRecord FromDocument(SyncDocument document) =>
        new()
        {
            Key = MakeKey(document.Collection, document.Id),
            Collection = document.Collection,
            EntityId = document.Id,
            PayloadJson = document.PayloadJson,
            Version = document.Version,
            CreatedAtUtc = document.CreatedAtUtc.ToString("O"),
            UpdatedAtUtc = document.UpdatedAtUtc.ToString("O"),
            IsDeleted = document.IsDeleted,
            SyncStateValue = (int)document.SyncState
        };
}

internal sealed class SyncChangeRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Collection { get; set; } = "";

    [Indexed]
    public string EntityId { get; set; } = "";

    public int Operation { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public long BaseVersion { get; set; }

    public string CreatedAtUtc { get; set; } = "";

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }

    public bool Force { get; set; }

    public bool IsFailed { get; set; }

    public PendingChange ToChange() =>
        new()
        {
            ChangeId = Id,
            Collection = Collection,
            EntityId = EntityId,
            Operation = (ChangeOperation)Operation,
            PayloadJson = PayloadJson,
            BaseVersion = BaseVersion,
            CreatedAtUtc = DateTimeOffset.Parse(CreatedAtUtc),
            AttemptCount = AttemptCount,
            LastError = LastError,
            Force = Force,
            IsFailed = IsFailed
        };
}

internal sealed class SyncCollectionMetaRecord
{
    [PrimaryKey]
    public string Collection { get; set; } = "";

    public string? Cursor { get; set; }

    public string? LastSyncAtUtc { get; set; }
}
