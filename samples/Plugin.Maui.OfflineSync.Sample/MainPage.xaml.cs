using Plugin.Maui.OfflineSync.Sample.ViewModels;

namespace Plugin.Maui.OfflineSync.Sample;

public partial class MainPage : ContentPage
{
    public MainPage()
        : this(IPlatformApplication.Current!.Services.GetRequiredService<MainViewModel>())
    {
    }

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MainViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }
}
