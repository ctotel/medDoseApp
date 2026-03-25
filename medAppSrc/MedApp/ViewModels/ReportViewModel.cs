using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MedApp.Models;
using MedApp.Services;

namespace MedApp.ViewModels;

public partial class ReportViewModel : ObservableObject
{
    private readonly ReportService _reportService;

    public ReportViewModel(ReportService reportService)
    {
        _reportService = reportService;
    }

    [ObservableProperty]
    private bool _isWeekly;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private ReportData? _report;

    [ObservableProperty]
    private bool _hasReport;

    [ObservableProperty]
    private string _statusMessage = "Select a date and tap Generate.";

    partial void OnIsWeeklyChanged(bool value) => HasReport = false;
    partial void OnSelectedDateChanged(DateTime value) => HasReport = false;

    [RelayCommand]
    private async Task GenerateAsync()
    {
        StatusMessage = "Generating...";

        Report = IsWeekly
            ? await _reportService.GetWeeklyReportAsync(SelectedDate)
            : await _reportService.GetDailyReportAsync(SelectedDate);

        HasReport = true;
        StatusMessage = Report.TotalDoses == 0
            ? "No dose data for this period."
            : $"{Report.AdherencePercent}% adherence ({Report.TakenCount}/{Report.TotalDoses} taken)";
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (Report is null) return;

        try
        {
            var path = _reportService.GeneratePdf(Report);

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "MedApp Report",
                File = new ShareFile(path)
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"PDF export failed: {ex.Message}";
        }
    }
}
