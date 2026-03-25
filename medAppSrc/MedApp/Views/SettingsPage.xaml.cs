using MedApp.ViewModels;

namespace MedApp.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _vm;

    public SettingsPage()
    {
        InitializeComponent();
        _vm = IPlatformApplication.Current!.Services.GetRequiredService<SettingsViewModel>();
        BindingContext = _vm;
    }

    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.CheckPermissionCommand.Execute(null);
    }
}
