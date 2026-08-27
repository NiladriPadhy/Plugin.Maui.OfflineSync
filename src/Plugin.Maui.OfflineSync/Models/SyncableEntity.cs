namespace Plugin.Maui.OfflineSync;

/// <summary>
/// Base type for documents that participate in offline-first synchronization.
/// </summary>
public abstract class SyncableEntity
{
    /// <summary>
    /// Stable document identifier. Generated automatically on first insert when empty.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Monotonic version assigned by the remote (or incremented locally before first sync).
    /// </summary>
    [JsonPropertyName("version")]
    public long Version { get; set; }

    [JsonPropertyName("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("updatedAtUtc")]
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Local-only sync state. Not sent to the remote.
    /// </summary>
    [JsonIgnore]
    public SyncState SyncState { get; set; } = SyncState.PendingCreate;
}
