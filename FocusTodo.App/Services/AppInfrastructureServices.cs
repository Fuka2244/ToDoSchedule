using System.IO;
using System.Windows;
using System.Windows.Threading;
using FocusTodo.App;
using FocusTodo.App.Data;
using FocusTodo.App.Models;
using FocusTodo.App.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Forms = System.Windows.Forms;

namespace FocusTodo.App.Services;

public sealed class ReminderService(
    IServiceScopeFactory scopeFactory,
    INotificationService notificationService,
    ICountdownService countdownService,
    ILoggingService loggingService,
    IRecurrenceService? recurrenceService = null,
    ILocalizationService? localizationService = null) : IReminderService
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private DateTime? _pausedUntil;
    private bool _started;
    private bool _isChecking;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return Task.CompletedTask;
        }

        _started = true;
        _timer.Tick += async (_, _) => await CheckRemindersAsync();
        _timer.Start();
        loggingService.LogInfo("Reminder service started. Scan interval: 5 seconds.");
        _ = CheckRemindersAsync();
        return Task.CompletedTask;
    }

    public Task CheckNowAsync(CancellationToken cancellationToken = default)
    {
        return CheckRemindersAsync();
    }

    public void PauseFor(TimeSpan duration)
    {
        _pausedUntil = DateTime.Now.Add(duration);
        loggingService.LogInfo($"Reminders paused until {_pausedUntil:O}.");
    }

    public void Resume()
    {
        _pausedUntil = null;
        loggingService.LogInfo("Reminders resumed.");
        _ = CheckRemindersAsync();
    }

    private async Task CheckRemindersAsync()
    {
        if (_isChecking)
        {
            return;
        }

        if (_pausedUntil is not null && _pausedUntil > DateTime.Now)
        {
            return;
        }

        _pausedUntil = null;
        _isChecking = true;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITodoRepository>();
            var now = DateTime.Now;
            var reminders = await repository.GetDueReminderTodosAsync(now, 3);
            loggingService.LogInfo($"Reminder scan at {now:O}. Due reminders: {reminders.Count}.");

            foreach (var item in reminders)
            {
                var nextCount = item.ReminderSentCount + 1;
                var body = $"{item.Title}\n{countdownService.GetCountdownText(item, now)}\n{string.Format(T("ReminderCount"), nextCount, item.ReminderMaxCount)}";
                await notificationService.ShowAsync(T("ReminderTitle"), body);
                loggingService.LogInfo($"Reminder sent. TodoId={item.Id}, Title={item.Title}, Count={nextCount}/{item.ReminderMaxCount}.");

                if (item.RepeatType != RepeatType.None && nextCount >= item.ReminderMaxCount)
                {
                    AdvanceRecurringReminder(item, now);
                }
                else
                {
                    item.ReminderSentCount = nextCount;
                    item.LastReminderAt = now;
                    item.SnoozedUntil = null;
                    item.NextReminderAt = nextCount >= item.ReminderMaxCount
                        ? null
                        : now.AddMinutes(Math.Max(1, item.ReminderIntervalMinutes));
                }

                item.UpdatedAt = now;
            }

            if (reminders.Count > 0)
            {
                await repository.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            loggingService.LogError(ex, "Reminder check failed");
        }
        finally
        {
            _isChecking = false;
        }
    }

    private void AdvanceRecurringReminder(TodoItem item, DateTime now)
    {
        var nextDueAt = GetNextFutureOccurrence(item.DueAt, item.RepeatType, item.RepeatInterval, now);
        item.ReminderSentCount = 0;
        item.LastReminderAt = now;
        item.SnoozedUntil = null;

        if (nextDueAt is null)
        {
            item.NextReminderAt = null;
            return;
        }

        item.DueAt = nextDueAt;
        item.NextReminderAt = nextDueAt.Value.AddMinutes(-Math.Max(0, item.ReminderLeadMinutes));
        loggingService.LogInfo($"Recurring reminder advanced. TodoId={item.Id}, NextDueAt={item.DueAt:O}, NextReminderAt={item.NextReminderAt:O}.");
    }

    private DateTime? GetNextFutureOccurrence(DateTime? dueAt, RepeatType repeatType, int interval, DateTime now)
    {
        var service = recurrenceService ?? new RecurrenceService();
        var next = service.GetNextOccurrence(dueAt, repeatType, interval);
        for (var i = 0; i < 100 && next is not null && next.Value <= now; i++)
        {
            next = service.GetNextOccurrence(next, repeatType, interval);
        }

        return next;
    }

    private string T(string key)
    {
        return localizationService?.Get(key) ?? key switch
        {
            "ReminderCount" => "Reminder {0}/{1}",
            "ReminderTitle" => "FocusTodo reminder",
            _ => key
        };
    }
}

public sealed class RecurrenceService : IRecurrenceService
{
    public DateTime? GetNextOccurrence(DateTime? dueAt, RepeatType repeatType, int interval)
    {
        if (dueAt is null || repeatType == RepeatType.None)
        {
            return null;
        }

        var safeInterval = Math.Max(1, interval);
        return repeatType switch
        {
            RepeatType.Secondly => dueAt.Value.AddSeconds(safeInterval),
            RepeatType.Minutely => dueAt.Value.AddMinutes(safeInterval),
            RepeatType.Daily => dueAt.Value.AddDays(safeInterval),
            RepeatType.Weekly => dueAt.Value.AddDays(7 * safeInterval),
            RepeatType.Monthly => dueAt.Value.AddMonths(safeInterval),
            RepeatType.Weekdays => NextWeekday(dueAt.Value),
            RepeatType.Custom => dueAt.Value.AddDays(safeInterval),
            _ => null
        };
    }

    private static DateTime NextWeekday(DateTime value)
    {
        var next = value.AddDays(1);
        while (next.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            next = next.AddDays(1);
        }

        return next;
    }
}

public sealed class NotificationService(ITrayService trayService, IDialogService dialogService, ILoggingService loggingService) : INotificationService
{
    public Task ShowAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            trayService.ShowNotification(title, message);
        }
        catch (Exception ex)
        {
            loggingService.LogError(ex, "Tray notification failed, falling back to in-app dialog");
            System.Windows.Application.Current.Dispatcher.Invoke(() => dialogService.ShowInfo(title, message));
        }

        return Task.CompletedTask;
    }
}

public sealed class TrayService(IServiceProvider serviceProvider, ILoggingService loggingService, ILocalizationService? localizationService = null) : ITrayService, IDisposable
{
    private Forms.NotifyIcon? _notifyIcon;

    public void Initialize()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(T("OpenFocusTodo"), null, (_, _) => ShowMainWindow());
        menu.Items.Add(T("QuickAddTodo"), null, async (_, _) => await QuickAddTodoAsync());
        menu.Items.Add(T("TestNotification"), null, (_, _) => ShowNotification("FocusTodo", "Notifications are working."));
        menu.Items.Add(T("PauseReminders"), null, (_, _) => serviceProvider.GetRequiredService<IReminderService>().PauseFor(TimeSpan.FromHours(1)));
        menu.Items.Add(T("ResumeReminders"), null, (_, _) => serviceProvider.GetRequiredService<IReminderService>().Resume());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "FocusTodo",
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
        loggingService.LogInfo("Tray service initialized.");
    }

    public void ShowNotification(string title, string message)
    {
        Initialize();
        _notifyIcon?.ShowBalloonTip(8000, title, message, Forms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }

    private static void ShowMainWindow()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var window = System.Windows.Application.Current.MainWindow;
            if (window is null)
            {
                return;
            }

            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        });
    }

    private async Task QuickAddTodoAsync()
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var todoService = scope.ServiceProvider.GetRequiredService<ITodoService>();
            await todoService.CreateRootTodoAsync($"Quick todo {DateTime.Now:HH:mm}", null, TodoPriority.Normal);
            ShowNotification("FocusTodo", "Quick todo added.");
            ShowMainWindow();
        }
        catch (Exception ex)
        {
            loggingService.LogError(ex, "Quick add todo failed");
        }
    }

    private static void ExitApplication()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            System.Windows.Application.Current.Properties["ExitRequested"] = true;
            System.Windows.Application.Current.Shutdown();
        });
    }

    private string T(string key)
    {
        return localizationService?.Get(key) ?? key switch
        {
            "OpenFocusTodo" => "Open FocusTodo",
            "QuickAddTodo" => "Quick Add Todo",
            "TestNotification" => "Test notification",
            "PauseReminders" => "Pause reminders 1 hour",
            "ResumeReminders" => "Resume reminders",
            _ => key
        };
    }
}

public sealed class StartupService : IStartupService
{
    public bool IsLaunchAtStartupEnabled()
    {
        return false;
    }
}

public sealed class PinnedWindowService(
    IServiceScopeFactory scopeFactory,
    ICountdownService countdownService,
    ILoggingService loggingService,
    ILocalizationService? localizationService = null) : IPinnedWindowService
{
    private readonly Dictionary<Guid, PinnedTodoWindow> _windows = [];

    public async Task OpenPinnedWindowAsync(Guid todoItemId, CancellationToken cancellationToken = default)
    {
        if (_windows.TryGetValue(todoItemId, out var existing))
        {
            existing.Show();
            existing.Activate();
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FocusTodoDbContext>();
        var todo = await dbContext.TodoItems
            .AsNoTracking()
            .Include(x => x.Children.OrderBy(c => c.SortOrder))
            .FirstOrDefaultAsync(x => x.Id == todoItemId, cancellationToken);
        if (todo is null)
        {
            await UnpinAsync(todoItemId, cancellationToken);
            return;
        }

        var setting = await dbContext.PinnedWindowSettings.FirstOrDefaultAsync(x => x.TodoItemId == todoItemId, cancellationToken);
        if (setting is null)
        {
            setting = new PinnedWindowSetting
            {
                TodoItemId = todoItemId,
                Left = 120 + (_windows.Count * 30),
                Top = 120 + (_windows.Count * 30),
                Width = 330,
                Height = 430,
                Opacity = 0.95,
                IsTopMost = true,
                IsLocked = false,
                ShowCompleted = true
            };
            await dbContext.PinnedWindowSettings.AddAsync(setting, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var viewModel = new ViewModels.PinnedTodoViewModel(todo, setting, scopeFactory, countdownService, this, loggingService, localizationService);
        var window = new PinnedTodoWindow(viewModel);
        window.Closed += (_, _) => _windows.Remove(todoItemId);
        _windows[todoItemId] = window;
        window.Show();
    }

    public async Task OpenSavedWindowsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FocusTodoDbContext>();
        var ids = await dbContext.PinnedWindowSettings
            .OrderBy(x => x.Id)
            .Select(x => x.TodoItemId)
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
        {
            await OpenPinnedWindowAsync(id, cancellationToken);
        }
    }

    public async Task SaveSettingAsync(PinnedWindowSetting setting, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FocusTodoDbContext>();
        var existing = await dbContext.PinnedWindowSettings.FirstOrDefaultAsync(x => x.Id == setting.Id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.Left = setting.Left;
        existing.Top = setting.Top;
        existing.Width = setting.Width;
        existing.Height = setting.Height;
        existing.Opacity = setting.Opacity;
        existing.IsTopMost = setting.IsTopMost;
        existing.IsLocked = setting.IsLocked;
        existing.ShowCompleted = setting.ShowCompleted;
        existing.UpdatedAt = DateTime.Now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UnpinAsync(Guid todoItemId, CancellationToken cancellationToken = default)
    {
        if (_windows.TryGetValue(todoItemId, out var window))
        {
            _windows.Remove(todoItemId);
            window.ForceClose();
        }

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FocusTodoDbContext>();
        var settings = await dbContext.PinnedWindowSettings
            .Where(x => x.TodoItemId == todoItemId)
            .ToListAsync(cancellationToken);
        dbContext.PinnedWindowSettings.RemoveRange(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        loggingService.LogInfo($"Todo unpinned. TodoId={todoItemId}.");
    }
}

public sealed class NavigationService : INavigationService
{
    public void NavigateTo(string route)
    {
    }
}

public sealed class DialogService : IDialogService
{
    public bool Confirm(string title, string message)
    {
        return System.Windows.MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public void ShowInfo(string title, string message)
    {
        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowError(string title, string message)
    {
        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

public sealed class LoggingService : ILoggingService
{
    private readonly string _logDirectory = Path.Combine(Environment.CurrentDirectory, "Logs");

    public void LogError(Exception exception, string message)
    {
        Write($"ERROR {message}{Environment.NewLine}{exception}");
    }

    public void LogInfo(string message)
    {
        Write($"INFO {message}");
    }

    private void Write(string line)
    {
        Directory.CreateDirectory(_logDirectory);
        var path = Path.Combine(_logDirectory, $"{DateTime.Now:yyyyMMdd}.log");
        File.AppendAllText(path, $"[{DateTime.Now:O}] {line}{Environment.NewLine}");
    }
}
