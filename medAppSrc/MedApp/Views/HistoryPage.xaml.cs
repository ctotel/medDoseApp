using MedApp.ViewModels;

namespace MedApp.Views;

public partial class HistoryPage : ContentPage
{
    private readonly DoseHistoryViewModel _vm;

    public HistoryPage()
    {
        InitializeComponent();
        _vm = IPlatformApplication.Current!.Services.GetRequiredService<DoseHistoryViewModel>();
        BindingContext = _vm;
    }

    public HistoryPage(DoseHistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCommand.Execute(null);
    }
}
