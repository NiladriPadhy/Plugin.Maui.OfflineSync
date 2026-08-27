namespace Plugin.Maui.OfflineSync;

internal static class SyncJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidOperationException("Failed to deserialize synchronized document.");
}

internal static class EntityMapper
{
    public static SyncDocument ToDocument<T>(string collection, T entity) where T : SyncableEntity =>
        new()
        {
            Collection = collection,
            Id = entity.Id,
            PayloadJson = SyncJson.Serialize(entity),
            Version = entity.Version,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            IsDeleted = entity.IsDeleted,
            SyncState = entity.SyncState
        };

    public static T ToEntity<T>(SyncDocument document) where T : SyncableEntity, new()
    {
        var entity = SyncJson.Deserialize<T>(document.PayloadJson);
        entity.Id = document.Id;
        entity.Version = document.Version;
        entity.CreatedAtUtc = document.CreatedAtUtc;
        entity.UpdatedAtUtc = document.UpdatedAtUtc;
        entity.IsDeleted = document.IsDeleted;
        entity.SyncState = document.SyncState;
        return entity;
    }
}
