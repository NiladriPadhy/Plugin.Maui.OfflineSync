# Plugin.Maui.OfflineSync

Offline-first data synchronization engine for .NET MAUI on **iOS** and **Android**.

Local writes are persisted immediately (SQLite by default), queued in a change log, and synchronized when the device is online. Conflicts are resolved with a built-in strategy or your own merger.

## Install

```bash
dotnet add package Plugin.Maui.OfflineSync
```

Target frameworks:

- `net10.0` (unit tests / shared)
- `net10.0-android`
- `net10.0-ios`

## Quick start

```csharp
builder
    .UseMauiApp<App>()
    .UseOfflineSync(options =>
    {
        options.RemoteBaseAddress = new Uri("https://api.example.com/sync/");
        options.ConflictStrategy = ConflictStrategy.LastWriteWins;
        options.AutoSync = true;
        options.AutoSyncInterval = TimeSpan.FromMinutes(5);
    });
```

```csharp
public sealed class TodoItem : SyncableEntity
{
    public string Title { get; set; } = "";
    public bool IsDone { get; set; }
}

var todos = OfflineSync.Default.GetCollection<TodoItem>("todos");
await todos.InsertAsync(new TodoItem { Title = "Buy milk" });
await OfflineSync.Default.SyncAsync();
```

`InsertAsync` / `UpdateAsync` / `DeleteAsync` always succeed locally. `SyncAsync` pushes the change log and pulls remote updates.

## What you get

- Local-first CRUD against SQLite (or an in-memory store)
- Automatic change tracking with coalescing (edit-after-insert stays one insert)
- Bidirectional sync with cursor-based incremental pull
- Conflict strategies: last-write-wins, server-wins, client-wins, or custom
- Network-aware auto-sync and optional OS background sync
- Pluggable remote: HTTP, in-memory (tests/demos), or your own `IRemoteSyncClient`

## HTTP protocol

`HttpRemoteSyncClient` uses a small conventional API.

**Pull**

`GET {base}/{collection}?cursor={cursor}`

```json
{
  "items": [
    {
      "id": "abc",
      "version": 4,
      "updatedAtUtc": "2026-08-27T12:00:00Z",
      "isDeleted": false,
      "payloadJson": "{\"id\":\"abc\",\"title\":\"Buy milk\"}"
    }
  ],
  "cursor": "6389...",
  "serverTimeUtc": "2026-08-27T12:00:01Z"
}
```

**Push**

`POST {base}/{collection}/changes`

```json
{
  "changes": [
    {
      "id": "abc",
      "operation": "insert",
      "baseVersion": 0,
      "updatedAtUtc": "2026-08-27T11:59:00Z",
      "payloadJson": "{\"id\":\"abc\",\"title\":\"Buy milk\"}"
    }
  ]
}
```

Response:

```json
{
  "accepted": [{ "id": "abc", "version": 1, "updatedAtUtc": "2026-08-27T12:00:00Z" }],
  "conflicts": [],
  "rejected": []
}
```

A conflict should include the server document (`serverVersion`, `serverUpdatedAtUtc`, `serverPayloadJson`). The engine then applies `IConflictResolver`.

To supply your own transport:

```csharp
builder.Services.AddSingleton<IRemoteSyncClient, MyApiClient>();
```

## Background sync

Set `EnableBackgroundSync = true`.

**Android** — the package registers a `JobScheduler` job and requests `RECEIVE_BOOT_COMPLETED`. The host app should also declare that permission.

**iOS** — add the task identifier to `Info.plist`:

```xml
<key>BGTaskSchedulerPermittedIdentifiers</key>
<array>
  <string>plugin.maui.offlinesync.refresh</string>
</array>
<key>UIBackgroundModes</key>
<array>
  <string>fetch</string>
  <string>processing</string>
</array>
```

The identifier is `OfflineSyncBackground.iOSTaskIdentifier`.

## Sample

`samples/Plugin.Maui.OfflineSync.Sample` is an iOS/Android todo app that writes to SQLite and syncs through an in-process remote. Toggle **Offline** to queue changes, then **Sync now**.

## Pack

```bash
dotnet pack src/Plugin.Maui.OfflineSync/Plugin.Maui.OfflineSync.csproj -c Release
```

The `.nupkg` is written to `artifacts/`.

## Support

> If this plugin saved you a weekend of native plumbing, consider buying me a coffee.
> Your support keeps it maintained, documented, and free.

[![Buy Me A Coffee](https://img.shields.io/badge/Buy%20Me%20a%20Coffee-ffdd00?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black)](https://buymeacoffee.com/npadhy)

This library stays open source. A coffee helps cover time for bug fixes, new features, and docs.
