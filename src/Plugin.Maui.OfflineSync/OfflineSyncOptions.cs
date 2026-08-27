namespace Plugin.Maui.OfflineSync;

/// <summary>
/// Configuration for the offline-first synchronization engine.
/// </summary>
public sealed class OfflineSyncOptions
{
    /// <summary>
    /// File name used when <see cref="DatabasePath"/> is not set. Default: offlinesync.db3
    /// </summary>
    public string DatabaseFileName { get; set; } = "offlinesync.db3";

    /// <summary>
    /// Absolute path to the SQLite database. When null, the file is created under AppDataDirectory.
    /// </summary>
    public string? DatabasePath { get; set; }

    /// <summary>
    /// Use an in-memory store instead of SQLite. Intended for tests and demos.
    /// </summary>
    public bool UseInMemoryStore { get; set; }

    /// <summary>
    /// Automatically start periodic and connectivity-triggered sync after the app launches.
    /// </summary>
    public bool AutoSync { get; set; } = true;

    /// <summary>
    /// Interval between automatic foreground sync attempts.
    /// </summary>
    public TimeSpan AutoSyncInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Sync as soon as connectivity is restored.
    /// </summary>
    public bool SyncOnNetworkRestored { get; set; } = true;

    /// <summary>
    /// Sync when the app returns to the foreground.
    /// </summary>
    public bool SyncOnResume { get; set; } = true;

    /// <summary>
    /// Register a platform background task (Android JobScheduler / iOS BGTaskScheduler).
    /// The host app must declare the required manifest/Info.plist entries.
    /// </summary>
    public bool EnableBackgroundSync { get; set; }

    /// <summary>
    /// How local vs remote conflicts are resolved.
    /// </summary>
    public ConflictStrategy ConflictStrategy { get; set; } = ConflictStrategy.LastWriteWins;

    /// <summary>
    /// Optional custom resolver used when <see cref="ConflictStrategy"/> is <see cref="ConflictStrategy.Custom"/>.
    /// </summary>
    public IConflictResolver? CustomConflictResolver { get; set; }

    /// <summary>
    /// Explicit remote client instance. Takes precedence over <see cref="RemoteBaseAddress"/>.
    /// </summary>
    public IRemoteSyncClient? RemoteClient { get; set; }

    /// <summary>
    /// Base URI for the default HTTP remote client, e.g. https://api.example.com/sync
    /// </summary>
    public Uri? RemoteBaseAddress { get; set; }

    /// <summary>
    /// Optional bearer token used by <see cref="HttpRemoteSyncClient"/>.
    /// </summary>
    public string? RemoteAccessToken { get; set; }

    /// <summary>
    /// Optional async token provider used by <see cref="HttpRemoteSyncClient"/>.
    /// </summary>
    public Func<Task<string?>>? RemoteAccessTokenProvider { get; set; }

    /// <summary>
    /// Skip remote calls and keep data local only.
    /// </summary>
    public bool LocalOnly { get; set; }

    /// <summary>
    /// Maximum push attempts for a single change before it is marked <see cref="SyncState.Failed"/>.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 8;

    /// <summary>
    /// Base delay used for exponential backoff between automatic retries.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum number of pending changes sent in one push request.
    /// </summary>
    public int PushBatchSize { get; set; } = 50;

    /// <summary>
    /// Keep deleted documents as tombstones until a successful sync instead of removing them immediately.
    /// </summary>
    public bool UseSoftDelete { get; set; } = true;
}
