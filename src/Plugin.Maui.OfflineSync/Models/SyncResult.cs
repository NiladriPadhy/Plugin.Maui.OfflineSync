namespace Plugin.Maui.OfflineSync;

/// <summary>
/// Outcome of a push/pull cycle.
/// </summary>
public sealed class SyncResult
{
    public bool Succeeded { get; init; }

    public bool Skipped { get; init; }

    public string? Message { get; init; }

    public int Pushed { get; set; }

    public int Pulled { get; set; }

    public int Conflicts { get; set; }

    public int Failed { get; set; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public static SyncResult Skip(string message) =>
        new() { Skipped = true, Succeeded = true, Message = message };

    public static SyncResult Ok(int pushed, int pulled, int conflicts) =>
        new() { Succeeded = true, Pushed = pushed, Pulled = pulled, Conflicts = conflicts };

    public static SyncResult Fail(string message, IEnumerable<string>? errors = null) =>
        new()
        {
            Succeeded = false,
            Message = message,
            Errors = errors?.ToArray() ?? [message]
        };
}
