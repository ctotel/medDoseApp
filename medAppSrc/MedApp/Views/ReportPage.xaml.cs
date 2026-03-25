using MedApp.ViewModels;

namespace MedApp.Views;

public partial class ReportPage : ContentPage
{
    public ReportPage()
    {
        InitializeComponent();
        BindingContext = IPlatformApplication.Current!.Services.GetRequiredService<ReportViewModel>();
    }

    public ReportPage(ReportViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
