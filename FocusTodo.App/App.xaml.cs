using System.IO;
using System.Windows;
using FocusTodo.App.Data;
using FocusTodo.App.Repositories;
using FocusTodo.App.Services;
using FocusTodo.App.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FocusTodo.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            _services = ConfigureServices();
            await using var scope = _services.CreateAsyncScope();
            var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
            await initializer.InitializeAsync();

            var mainWindow = _services.GetRequiredService<MainWindow>();
            mainWindow.Show();
            mainWindow.Activate();

            _services.GetRequiredService<ITrayService>().Initialize();
            await _services.GetRequiredService<IReminderService>().StartAsync();
            await _services.GetRequiredService<IPinnedWindowService>().OpenSavedWindowsAsync();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            System.Windows.Forms.MessageBox.Show(
                $"FocusTodo failed to start.\n\n{ex.Message}\n\nSee .\\Logs\\crash-{DateTime.Now:yyyyMMdd}.log",
                "FocusTodo startup error",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider ConfigureServices()
    {
        Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "Logs"));

        var services = new ServiceCollection();
        var preferencesService = new AppPreferencesService();
        Directory.CreateDirectory(preferencesService.Preferences.DbDirectory);
        var dbPath = Path.Combine(preferencesService.Preferences.DbDirectory, "focusTodo.db");
        services.AddSingleton<IAppPreferencesService>(preferencesService);
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddDbContext<FocusTodoDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
        services.AddScoped<ITodoRepository, TodoRepository>();
        services.AddScoped<ITodoService, TodoService>();
        services.AddSingleton<ICountdownService, CountdownService>();
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IReminderService, ReminderService>();
        services.AddSingleton<IRecurrenceService, RecurrenceService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<ITrayService, TrayService>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<IPinnedWindowService, PinnedWindowService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();
        return services.BuildServiceProvider();
    }

    private static void WriteCrashLog(Exception exception)
    {
        var logDirectory = Path.Combine(Environment.CurrentDirectory, "Logs");
        Directory.CreateDirectory(logDirectory);
        var path = Path.Combine(logDirectory, $"crash-{DateTime.Now:yyyyMMdd}.log");
        File.AppendAllText(path, $"[{DateTime.Now:O}] {exception}{Environment.NewLine}");
    }
}
