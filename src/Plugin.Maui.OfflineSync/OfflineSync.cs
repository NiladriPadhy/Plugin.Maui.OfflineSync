namespace Plugin.Maui.OfflineSync;

/// <summary>
/// Static entry point for the default <see cref="IOfflineSyncEngine"/> instance.
/// </summary>
public static class OfflineSync
{
    private static IOfflineSyncEngine? _default;

    /// <summary>
    /// The engine registered by <see cref="MauiAppBuilderExtensions.UseOfflineSync"/>.
    /// </summary>
    public static IOfflineSyncEngine Default =>
        _default ?? throw new InvalidOperationException(
            "OfflineSync has not been initialized. Call builder.UseOfflineSync() in MauiProgram.");

    internal static void SetDefault(IOfflineSyncEngine engine) => _default = engine;

    internal static bool IsInitialized => _default is not null;
}
