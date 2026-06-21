namespace FocusTodo.App.Services;

public interface IReminderService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task CheckNowAsync(CancellationToken cancellationToken = default);
    void PauseFor(TimeSpan duration);
    void Resume();
}

public interface IRecurrenceService
{
    DateTime? GetNextOccurrence(DateTime? dueAt, Models.RepeatType repeatType, int interval);
}

public interface INotificationService
{
    Task ShowAsync(string title, string message, CancellationToken cancellationToken = default);
}

public interface ITrayService
{
    void Initialize();
    void ShowNotification(string title, string message);
}

public interface IStartupService
{
    bool IsLaunchAtStartupEnabled();
}

public interface IPinnedWindowService
{
    Task OpenPinnedWindowAsync(Guid todoItemId, CancellationToken cancellationToken = default);
    Task OpenSavedWindowsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingAsync(Models.PinnedWindowSetting setting, CancellationToken cancellationToken = default);
    Task UnpinAsync(Guid todoItemId, CancellationToken cancellationToken = default);
}

public interface INavigationService
{
    void NavigateTo(string route);
}

public interface IDialogService
{
    bool Confirm(string title, string message);
    void ShowInfo(string title, string message);
    void ShowError(string title, string message);
}

public interface ILoggingService
{
    void LogError(Exception exception, string message);
    void LogInfo(string message);
}

public sealed class AppPreferences
{
    public string DbDirectory { get; set; } = string.Empty;
    public string Language { get; set; } = "zh-CN";
    public bool AutoCreateNextRecurringTodos { get; set; }
}

public interface IAppPreferencesService
{
    string ConfigDirectory { get; }
    AppPreferences Preferences { get; }
    Task SaveAsync(CancellationToken cancellationToken = default);
}

public interface ILocalizationService
{
    event EventHandler? LanguageChanged;
    string CurrentLanguage { get; }
    IReadOnlyList<string> LanguageOptions { get; }
    string Get(string key);
    void SetLanguage(string language);
}
