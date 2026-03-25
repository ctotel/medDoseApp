using CommunityToolkit.Maui;
using MedApp.Data;
using MedApp.Services;
using MedApp.ViewModels;
using MedApp.Views;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;

namespace MedApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseLocalNotification(config =>
            {
                config.AddCategory(new NotificationCategory(NotificationCategoryType.Alarm)
                {
                    ActionList = new HashSet<NotificationAction>(new[]
                    {
                        new NotificationAction(100) { Title = "Take",   Android = { LaunchAppWhenTapped = true } },
                        new NotificationAction(101) { Title = "Snooze", Android = { LaunchAppWhenTapped = false } },
                        new NotificationAction(102) { Title = "Skip",   Android = { LaunchAppWhenTapped = false } }
                    })
                });
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ── Data layer (singletons — one DB connection for app lifetime) ──────
        builder.Services.AddSingleton<DatabaseContext>();
        builder.Services.AddSingleton<MedicationRepository>();
        builder.Services.AddSingleton<DoseLogRepository>();

        // ── Services (singletons — shared notification & report engines) ──────
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<INotificationDispatcher, MauiNotificationDispatcher>();
        builder.Services.AddSingleton<IPreferencesProvider, MauiPreferencesProvider>();
        builder.Services.AddSingleton<NotificationService>();
        builder.Services.AddSingleton<ReportService>();

        // ── ViewModels (transient — fresh instance per navigation) ────────────
        builder.Services.AddTransient<MedicationListViewModel>();
        builder.Services.AddTransient<AddEditMedicationViewModel>();
        builder.Services.AddTransient<TodayViewModel>();
        builder.Services.AddTransient<DoseHistoryViewModel>();
        builder.Services.AddTransient<ReportViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // ── Pages (transient — fresh instance per navigation) ─────────────────
        builder.Services.AddTransient<TodayPage>();
        builder.Services.AddTransient<MedicationListPage>();
        builder.Services.AddTransient<AddEditMedicationPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<ReportPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
