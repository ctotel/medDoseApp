using MedApp.ViewModels;

namespace MedApp.Views;

public partial class TodayPage : ContentPage
{
    private readonly TodayViewModel _vm;

    public TodayPage()
    {
        InitializeComponent();
        _vm = IPlatformApplication.Current!.Services.GetRequiredService<TodayViewModel>();
        BindingContext = _vm;
    }

    public TodayPage(TodayViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCommand.Execute(null);
        _vm.StartAutoRefresh(Dispatcher);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.StopAutoRefresh();
    }
}
