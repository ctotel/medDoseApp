using MedApp.ViewModels;

namespace MedApp.Views;

public partial class MedicationListPage : ContentPage
{
    private readonly MedicationListViewModel _vm;

    public MedicationListPage()
    {
        InitializeComponent();
        _vm = Handler?.MauiContext?.Services.GetRequiredService<MedicationListViewModel>()
            ?? IPlatformApplication.Current!.Services.GetRequiredService<MedicationListViewModel>();
        BindingContext = _vm;
    }

    public MedicationListPage(MedicationListViewModel vm)
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
