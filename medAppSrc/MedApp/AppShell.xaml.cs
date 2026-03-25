using MedApp.Views;

namespace MedApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for pages that are navigated to modally / by route
        // (not shown as tabs, so not declared in AppShell.xaml)
        Routing.RegisterRoute(nameof(AddEditMedicationPage), typeof(AddEditMedicationPage));
    }
}
