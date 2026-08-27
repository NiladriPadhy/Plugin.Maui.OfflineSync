using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Plugin.Maui.OfflineSync.Remote;

/// <summary>
/// Options for the conventional HTTP sync protocol.
/// </summary>
public sealed class HttpRemoteOptions
{
    public required Uri BaseAddress { get; init; }

    public string? AccessToken { get; set; }

    public Func<Task<string?>>? AccessTokenProvider { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public HttpMessageHandler? Handler { get; init; }
}

/// <summary>
/// Default REST transport.
/// GET {base}/{collection}?cursor={cursor}
/// POST {base}/{collection}/changes
/// </summary>
public sealed class HttpRemoteSyncClient : IRemoteSyncClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly HttpRemoteOptions _options;
    private readonly bool _ownsClient;

    public HttpRemoteSyncClient(HttpRemoteOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _http = options.Handler is null
            ? new HttpClient { BaseAddress = options.BaseAddress, Timeout = options.Timeout }
            : new HttpClient(options.Handler, disposeHandler: false) { BaseAddress = options.BaseAddress, Timeout = options.Timeout };
        _ownsClient = true;
    }

    public HttpRemoteSyncClient(HttpClient httpClient, HttpRemoteOptions options)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ownsClient = false;
        _http.BaseAddress ??= options.BaseAddress;
    }

    public async Task<PullResponse> PullAsync(string collection, string? cursor, CancellationToken cancellationToken = default)
    {
        await ApplyAuthAsync(cancellationToken).ConfigureAwait(false);

        var url = string.IsNullOrWhiteSpace(cursor)
            ? $"{collection.Trim('/')}"
            : $"{collection.Trim('/')}?cursor={Uri.EscapeDataString(cursor)}";

        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PullResponse>(SyncJson.Options, cancellationToken).ConfigureAwait(false)
               ?? new PullResponse();
    }

    public async Task<PushResponse> PushAsync(string collection, IReadOnlyList<PushChange> changes, CancellationToken cancellationToken = default)
    {
        await ApplyAuthAsync(cancellationToken).ConfigureAwait(false);

        var url = $"{collection.Trim('/')}/changes";
        using var response = await _http.PostAsJsonAsync(url, new { changes }, SyncJson.Options, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PushResponse>(SyncJson.Options, cancellationToken).ConfigureAwait(false)
               ?? new PushResponse();
    }

    private async Task ApplyAuthAsync(CancellationToken cancellationToken)
    {
        var token = _options.AccessToken;
        if (_options.AccessTokenProvider is not null)
        {
            token = await _options.AccessTokenProvider().ConfigureAwait(false) ?? token;
        }

        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);

        cancellationToken.ThrowIfCancellationRequested();
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }
    }
}
