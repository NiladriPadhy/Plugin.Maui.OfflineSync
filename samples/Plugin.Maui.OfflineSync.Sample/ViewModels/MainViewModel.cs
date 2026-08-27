using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Plugin.Maui.OfflineSync;
using Plugin.Maui.OfflineSync.Networking;
using Plugin.Maui.OfflineSync.Sample.Models;

namespace Plugin.Maui.OfflineSync.Sample.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IOfflineSyncEngine _engine;
    private readonly ISyncCollection<TodoItem> _todos;
    private readonly ManualNetworkMonitor _network;
    private string _newTitle = string.Empty;
    private string _statusText = "Idle";
    private string _pendingText = "0 pending";
    private bool _isOnline = true;
    private bool _isBusy;

    public MainViewModel(IOfflineSyncEngine engine, INetworkMonitor network)
    {
        _engine = engine;
        _network = network as ManualNetworkMonitor ?? new ManualNetworkMonitor();
        _todos = engine.GetCollection<TodoItem>("todos");
        _isOnline = _network.IsOnline;

        AddCommand = new Command(async () => await AddAsync(), () => !string.IsNullOrWhiteSpace(NewTitle) && !IsBusy);
        SyncCommand = new Command(async () => await SyncAsync(), () => !IsBusy);
        ToggleOnlineCommand = new Command(ToggleOnline);
        ToggleDoneCommand = new Command<TodoItem>(async item => await ToggleDoneAsync(item));
        DeleteCommand = new Command<TodoItem>(async item => await DeleteAsync(item));

        _engine.StatusChanged += OnStatusChanged;
        _engine.SyncCompleted += OnSyncCompleted;
        _engine.CollectionChanged += OnCollectionChanged;
        _network.ConnectivityChanged += OnConnectivityChanged;
    }

    public ObservableCollection<TodoItem> Items { get; } = [];

    public ICommand AddCommand { get; }

    public ICommand SyncCommand { get; }

    public ICommand ToggleOnlineCommand { get; }

    public ICommand ToggleDoneCommand { get; }

    public ICommand DeleteCommand { get; }

    public string NewTitle
    {
        get => _newTitle;
        set
        {
            if (Set(ref _newTitle, value))
            {
                (AddCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public string PendingText
    {
        get => _pendingText;
        private set => Set(ref _pendingText, value);
    }

    public bool IsOnline
    {
        get => _isOnline;
        private set => Set(ref _isOnline, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                (AddCommand as Command)?.ChangeCanExecute();
                (SyncCommand as Command)?.ChangeCanExecute();
            }
        }
    }

    public string ConnectivityLabel => IsOnline ? "Online" : "Offline";

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync()
    {
        await ReloadAsync();
        await RefreshPendingAsync();
        StatusText = _engine.Status.ToString();
    }

    private async Task AddAsync()
    {
        var title = NewTitle.Trim();
        if (title.Length == 0)
        {
            return;
        }

        await _todos.InsertAsync(new TodoItem { Title = title });
        NewTitle = string.Empty;
        await ReloadAsync();
        await RefreshPendingAsync();
    }

    private async Task ToggleDoneAsync(TodoItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.IsDone = !item.IsDone;
        await _todos.UpdateAsync(item);
        await ReloadAsync();
        await RefreshPendingAsync();
    }

    private async Task DeleteAsync(TodoItem? item)
    {
        if (item is null)
        {
            return;
        }

        await _todos.DeleteAsync(item.Id);
        await ReloadAsync();
        await RefreshPendingAsync();
    }

    private async Task SyncAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _engine.SyncCollectionAsync("todos");
            StatusText = result.Skipped
                ? result.Message ?? "Skipped"
                : result.Succeeded
                    ? $"Synced · pushed {result.Pushed} · pulled {result.Pulled}"
                    : result.Message ?? "Sync failed";
            await ReloadAsync();
            await RefreshPendingAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ToggleOnline()
    {
        _network.SetOnline(!_network.IsOnline);
        IsOnline = _network.IsOnline;
        OnPropertyChanged(nameof(ConnectivityLabel));
    }

    private async Task ReloadAsync()
    {
        var items = await _todos.GetAllAsync();
        Items.Clear();
        foreach (var item in items.OrderByDescending(todo => todo.CreatedAtUtc))
        {
            Items.Add(item);
        }
    }

    private async Task RefreshPendingAsync()
    {
        var pending = await _engine.GetPendingCountAsync("todos");
        PendingText = pending == 1 ? "1 pending change" : $"{pending} pending changes";
    }

    private void OnStatusChanged(object? sender, SyncStatusChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() => StatusText = e.Status.ToString());

    private void OnSyncCompleted(object? sender, SyncCompletedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await ReloadAsync();
            await RefreshPendingAsync();
        });

    private void OnCollectionChanged(object? sender, CollectionChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await ReloadAsync();
            await RefreshPendingAsync();
        });

    private void OnConnectivityChanged(object? sender, bool online) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsOnline = online;
            OnPropertyChanged(nameof(ConnectivityLabel));
        });

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _engine.StatusChanged -= OnStatusChanged;
        _engine.SyncCompleted -= OnSyncCompleted;
        _engine.CollectionChanged -= OnCollectionChanged;
        _network.ConnectivityChanged -= OnConnectivityChanged;
    }
}
