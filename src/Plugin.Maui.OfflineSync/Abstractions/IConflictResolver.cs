namespace Plugin.Maui.OfflineSync;

/// <summary>
/// Decides which side wins when local and remote versions of the same document diverge.
/// </summary>
public interface IConflictResolver
{
    ConflictResolution Resolve(ConflictContext context);
}
