using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusTodo.App.Models;
using FocusTodo.App.Services;

namespace FocusTodo.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ITodoService _todoService;
    private readonly ICountdownService _countdownService;
    private readonly IDialogService _dialogService;
    private readonly ILoggingService _loggingService;
    private readonly IPinnedWindowService _pinnedWindowService;
    private readonly IAppPreferencesService? _preferencesService;
    private readonly ILocalizationService? _localizationService;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private List<TodoItemViewModel> _allTodos = [];

    public MainViewModel(
        ITodoService todoService,
        ICountdownService countdownService,
        IDialogService dialogService,
        ILoggingService loggingService,
        IPinnedWindowService pinnedWindowService,
        IAppPreferencesService? preferencesService = null,
        ILocalizationService? localizationService = null)
    {
        _todoService = todoService;
        _countdownService = countdownService;
        _dialogService = dialogService;
        _loggingService = loggingService;
        _pinnedWindowService = pinnedWindowService;
        _preferencesService = preferencesService;
        _localizationService = localizationService;

        Priorities = new ObservableCollection<TodoPriority>(Enum.GetValues<TodoPriority>());
        RepeatTypes = new ObservableCollection<RepeatType>(Enum.GetValues<RepeatType>());
        Hours = new ObservableCollection<int>(Enumerable.Range(0, 24));
        Minutes = new ObservableCollection<int>(Enumerable.Range(0, 12).Select(x => x * 5));
        FilterOptions = [];
        LanguageOptions = new ObservableCollection<string>(_localizationService?.LanguageOptions ?? ["zh-CN", "en-US"]);

        SelectedPriority = TodoPriority.Normal;
        SelectedRepeatType = RepeatType.None;
        RepeatInterval = 1;
        ReminderLeadMinutes = 20;
        ReminderIntervalMinutes = 30;
        ReminderMaxCount = 3;
        SelectedDueHour = 18;
        SelectedDueMinute = 0;
        StorageDirectory = _preferencesService?.Preferences.DbDirectory ?? string.Empty;
        SelectedLanguage = _preferencesService?.Preferences.Language ?? "zh-CN";
        AutoCreateNextRecurringTodos = _preferencesService?.Preferences.AutoCreateNextRecurringTodos ?? false;
        StatusMessage = T("Ready");
        RefreshFilterOptions();

        if (_localizationService is not null)
        {
            _localizationService.LanguageChanged += (_, _) => RefreshLocalizedText();
        }

        _timer.Tick += (_, _) => RefreshCountdowns();
        _timer.Start();
    }

    public ObservableCollection<TodoItemViewModel> Todos { get; } = [];
    public ObservableCollection<TodoPriority> Priorities { get; }
    public ObservableCollection<RepeatType> RepeatTypes { get; }
    public ObservableCollection<int> Hours { get; }
    public ObservableCollection<int> Minutes { get; }
    public ObservableCollection<string> FilterOptions { get; }
    public ObservableCollection<string> LanguageOptions { get; }
    public bool IsDetailVisible => SelectedTodo is not null;
    public bool IsRightPanelVisible => IsDetailVisible || IsSettingsVisible;

    public string EditText => T("Edit");
    public string NewTodoTitleText => T("NewTodoTitle");
    public string AddBigTodoText => T("AddBigTodo");
    public string AddSmallTodoText => T("AddSmallTodo");
    public string PriorityText => T("Priority");
    public string DdlText => T("Ddl");
    public string DdlDateText => T("DdlDate");
    public string DdlTimeText => T("DdlTime");
    public string SearchByTitleText => T("SearchByTitle");
    public string TaskDetailText => T("TaskDetail");
    public string TitleText => T("Title");
    public string DescriptionText => T("Description");
    public string RepeatText => T("Repeat");
    public string EveryText => T("Every");
    public string EnableReminderText => T("EnableReminder");
    public string LeadText => T("Lead");
    public string IntervalText => T("Interval");
    public string CountText => T("Count");
    public string SnoozeNextReminderText => T("SnoozeNextReminder");
    public string CloseToTrayHintText => T("CloseToTrayHint");
    public string SaveText => T("Save");
    public string DeleteText => T("Delete");
    public string PinWindowText => T("PinWindow");
    public string ClearText => T("Clear");
    public string CloseText => T("Close");
    public string SettingsText => T("Settings");
    public string DatabaseLocationText => T("DatabaseLocation");
    public string LanguageText => T("Language");
    public string AutoCreateNextRecurringTodosText => T("AutoCreateNextRecurringTodos");

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddChildTodoCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(SnoozeSelectedCommand))]
    private TodoItemViewModel? selectedTodo;

    [ObservableProperty]
    private bool isSettingsVisible;

    [ObservableProperty]
    private string newTodoTitle = string.Empty;

    [ObservableProperty]
    private string selectedFilter = string.Empty;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private TodoPriority selectedPriority;

    [ObservableProperty]
    private RepeatType selectedRepeatType;

    [ObservableProperty]
    private int repeatInterval;

    [ObservableProperty]
    private bool reminderEnabled;

    [ObservableProperty]
    private int reminderLeadMinutes;

    [ObservableProperty]
    private int reminderIntervalMinutes;

    [ObservableProperty]
    private int reminderMaxCount;

    [ObservableProperty]
    private DateTime? selectedDueDate;

    [ObservableProperty]
    private int selectedDueHour;

    [ObservableProperty]
    private int selectedDueMinute;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private string storageDirectory = string.Empty;

    [ObservableProperty]
    private string selectedLanguage = "zh-CN";

    [ObservableProperty]
    private bool autoCreateNextRecurringTodos;

    partial void OnSelectedFilterChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedTodoChanged(TodoItemViewModel? value)
    {
        OnPropertyChanged(nameof(IsDetailVisible));
        OnPropertyChanged(nameof(IsRightPanelVisible));

        if (value is null)
        {
            return;
        }

        IsSettingsVisible = false;
        SelectedPriority = value.Priority;
        SelectedRepeatType = value.Model.RepeatType;
        RepeatInterval = Math.Max(1, value.Model.RepeatInterval);
        ReminderEnabled = value.Model.ReminderEnabled;
        ReminderLeadMinutes = Math.Max(0, value.Model.ReminderLeadMinutes);
        ReminderIntervalMinutes = Math.Max(1, value.Model.ReminderIntervalMinutes);
        ReminderMaxCount = Math.Max(1, value.Model.ReminderMaxCount);
        SelectedDueDate = value.DueAt?.Date;
        SelectedDueHour = value.DueAt?.Hour ?? 18;
        SelectedDueMinute = RoundMinute(value.DueAt?.Minute ?? 0);
    }

    partial void OnIsSettingsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRightPanelVisible));
    }

    [RelayCommand]
    public void SelectTodo(TodoItemViewModel item)
    {
        SelectedTodo = item;
    }

    [RelayCommand]
    public void CloseDetail()
    {
        SelectedTodo = null;
    }

    [RelayCommand]
    public void OpenSettings()
    {
        SelectedTodo = null;
        StorageDirectory = _preferencesService?.Preferences.DbDirectory ?? StorageDirectory;
        SelectedLanguage = _preferencesService?.Preferences.Language ?? SelectedLanguage;
        AutoCreateNextRecurringTodos = _preferencesService?.Preferences.AutoCreateNextRecurringTodos ?? AutoCreateNextRecurringTodos;
        IsSettingsVisible = true;
    }

    [RelayCommand]
    public void CloseSettings()
    {
        IsSettingsVisible = false;
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        if (_preferencesService is null)
        {
            return;
        }

        var normalizedDirectory = string.IsNullOrWhiteSpace(StorageDirectory)
            ? _preferencesService.ConfigDirectory
            : StorageDirectory.Trim();

        try
        {
            Directory.CreateDirectory(normalizedDirectory);
            _preferencesService.Preferences.DbDirectory = normalizedDirectory;
            _preferencesService.Preferences.Language = SelectedLanguage;
            _preferencesService.Preferences.AutoCreateNextRecurringTodos = AutoCreateNextRecurringTodos;
            _localizationService?.SetLanguage(SelectedLanguage);
            await _preferencesService.SaveAsync();
            StatusMessage = T("RestartRequired");
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Save settings failed");
            _dialogService.ShowError(T("OperationFailed"), ex.Message);
        }
    }

    [RelayCommand]
    public void ClearDueAt()
    {
        SelectedDueDate = null;
        SelectedDueHour = 18;
        SelectedDueMinute = 0;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            var models = await _todoService.GetRootTodosAsync();
            _allTodos = models.Select(x => new TodoItemViewModel(x, _countdownService)).ToList();
            ApplyFilters();
            StatusMessage = string.Format(T("LoadedTodos"), _allTodos.Count);
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Load todos failed");
            _dialogService.ShowError(T("OperationFailed"), ex.Message);
        }
    }

    [RelayCommand]
    public async Task AddRootTodoAsync(string? title)
    {
        await RunAndReloadAsync(async () =>
        {
            var normalizedTitle = NormalizeNewTitle(title);
            _loggingService.LogInfo($"Add big todo requested. Title length: {normalizedTitle.Length}.");
            await _todoService.CreateRootTodoAsync(normalizedTitle, BuildDueAt(), SelectedPriority);
            NewTodoTitle = string.Empty;
            StatusMessage = T("AddBigTodo");
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelectedTodo))]
    public async Task AddChildTodoAsync(string? title)
    {
        if (SelectedTodo is null)
        {
            return;
        }

        var parent = SelectedTodo.IsRoot ? SelectedTodo : _allTodos.FirstOrDefault(x => x.Children.Any(c => c.Id == SelectedTodo.Id));
        if (parent is null)
        {
            return;
        }

        await RunAndReloadAsync(async () =>
        {
            var normalizedTitle = string.IsNullOrWhiteSpace(title) ? T("NewSmallTodo") : title.Trim();
            _loggingService.LogInfo($"Add small todo requested. Title length: {normalizedTitle.Length}.");
            await _todoService.CreateChildTodoAsync(parent.Id, normalizedTitle, BuildDueAt(), SelectedPriority);
            NewTodoTitle = string.Empty;
            StatusMessage = T("AddSmallTodo");
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelectedTodo))]
    public async Task SaveSelectedAsync()
    {
        if (SelectedTodo is null)
        {
            return;
        }

        await RunAndReloadAsync(async () =>
        {
            await _todoService.UpdateTodoAsync(
                SelectedTodo.Id,
                SelectedTodo.Title,
                SelectedTodo.Description,
                BuildDueAt(),
                SelectedPriority,
                SelectedRepeatType,
                RepeatInterval,
                ReminderEnabled,
                ReminderLeadMinutes,
                ReminderIntervalMinutes,
                ReminderMaxCount);
            StatusMessage = T("Save");
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelectedTodo))]
    public async Task SnoozeSelectedAsync(string? minutesText)
    {
        if (SelectedTodo is null)
        {
            return;
        }

        var minutes = int.TryParse(minutesText, out var parsed) ? parsed : 10;

        await RunAndReloadAsync(async () =>
        {
            await _todoService.SnoozeReminderAsync(SelectedTodo.Id, minutes);
            StatusMessage = $"{T("SnoozeNextReminder")} {minutes}m";
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelectedTodo))]
    public async Task PinSelectedAsync()
    {
        if (SelectedTodo is null)
        {
            return;
        }

        var root = SelectedTodo.IsRoot ? SelectedTodo : _allTodos.FirstOrDefault(x => x.Children.Any(c => c.Id == SelectedTodo.Id));
        if (root is null)
        {
            return;
        }

        await RunAndReloadAsync(async () =>
        {
            await _pinnedWindowService.OpenPinnedWindowAsync(root.Id);
            StatusMessage = $"{T("PinWindow")} {root.Title}";
        });
    }

    [RelayCommand]
    public async Task ToggleCompletedAsync(TodoItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        await RunAndReloadAsync(async () =>
        {
            await _todoService.ToggleCompletedAsync(item.Id, item.IsCompleted);
            StatusMessage = item.IsCompleted ? T("Completed") : T("Ready");
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelectedTodo))]
    public async Task DeleteSelectedAsync()
    {
        if (SelectedTodo is null)
        {
            return;
        }

        if (!_dialogService.Confirm(T("DeleteTask"), string.Format(T("DeleteTaskConfirm"), SelectedTodo.Title)))
        {
            return;
        }

        await RunAndReloadAsync(async () =>
        {
            await _todoService.DeleteTodoAsync(SelectedTodo.Id);
            SelectedTodo = null;
            StatusMessage = T("TaskDeleted");
        });
    }

    private bool HasSelectedTodo()
    {
        return SelectedTodo is not null;
    }

    private static string NormalizeNewTitle(string? title)
    {
        var normalized = (title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Title cannot be empty. Type the title in the top input next to the priority selector.");
        }

        return normalized;
    }

    private async Task RunAndReloadAsync(Func<Task> action)
    {
        try
        {
            await action();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Operation failed");
            _dialogService.ShowError(T("OperationFailed"), ex.Message);
        }
    }

    private DateTime? BuildDueAt()
    {
        if (SelectedDueDate is null)
        {
            return null;
        }

        return SelectedDueDate.Value.Date
            .AddHours(Math.Clamp(SelectedDueHour, 0, 23))
            .AddMinutes(Math.Clamp(SelectedDueMinute, 0, 59));
    }

    private void ApplyFilters()
    {
        Todos.Clear();
        var now = DateTime.Now;
        IEnumerable<TodoItemViewModel> items = _allTodos;

        items = SelectedFilter switch
        {
            var filter when filter == T("Today") => items.Where(x => x.DueAt?.Date == now.Date || x.Children.Any(c => c.DueAt?.Date == now.Date)),
            var filter when filter == T("Future") => items.Where(x => x.DueAt > now || x.Children.Any(c => c.DueAt > now)),
            var filter when filter == T("Overdue") => items.Where(x => (!x.IsCompleted && x.DueAt < now) || x.Children.Any(c => !c.IsCompleted && c.DueAt < now)),
            var filter when filter == T("Completed") => items.Where(x => x.IsCompleted || x.Children.Any(c => c.IsCompleted)),
            _ => items
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            items = items.Where(x => Contains(x, SearchText));
        }

        foreach (var item in items)
        {
            Todos.Add(item);
        }
    }

    private static bool Contains(TodoItemViewModel item, string searchText)
    {
        return item.Title.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)
            || item.Children.Any(c => c.Title.Contains(searchText, StringComparison.CurrentCultureIgnoreCase));
    }

    private static int RoundMinute(int minute)
    {
        var rounded = (int)Math.Round(minute / 5.0, MidpointRounding.AwayFromZero) * 5;
        return Math.Clamp(rounded, 0, 55);
    }

    private void RefreshCountdowns()
    {
        var now = DateTime.Now;
        foreach (var item in _allTodos)
        {
            item.RefreshCountdown(now);
        }
    }

    private string T(string key)
    {
        return _localizationService?.Get(key) ?? key switch
        {
            "All" => "All",
            "Today" => "Today",
            "Future" => "Future",
            "Overdue" => "Overdue",
            "Completed" => "Completed",
            "Ready" => "Ready",
            "OperationFailed" => "Operation failed",
            "LoadedTodos" => "Loaded {0} big todos",
            "NewSmallTodo" => "New small todo",
            "DeleteTask" => "Delete task",
            "DeleteTaskConfirm" => "Delete \"{0}\"?",
            "TaskDeleted" => "Task deleted",
            "RestartRequired" => "Settings saved. Database folder changes take effect after restart.",
            _ => key
        };
    }

    private void RefreshFilterOptions()
    {
        var selected = SelectedFilter;
        FilterOptions.Clear();
        FilterOptions.Add(T("All"));
        FilterOptions.Add(T("Today"));
        FilterOptions.Add(T("Future"));
        FilterOptions.Add(T("Overdue"));
        FilterOptions.Add(T("Completed"));
        SelectedFilter = FilterOptions.Contains(selected) ? selected : T("All");
    }

    private void RefreshLocalizedText()
    {
        RefreshFilterOptions();
        OnPropertyChanged(nameof(EditText));
        OnPropertyChanged(nameof(NewTodoTitleText));
        OnPropertyChanged(nameof(AddBigTodoText));
        OnPropertyChanged(nameof(AddSmallTodoText));
        OnPropertyChanged(nameof(PriorityText));
        OnPropertyChanged(nameof(DdlText));
        OnPropertyChanged(nameof(DdlDateText));
        OnPropertyChanged(nameof(DdlTimeText));
        OnPropertyChanged(nameof(SearchByTitleText));
        OnPropertyChanged(nameof(TaskDetailText));
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(RepeatText));
        OnPropertyChanged(nameof(EveryText));
        OnPropertyChanged(nameof(EnableReminderText));
        OnPropertyChanged(nameof(LeadText));
        OnPropertyChanged(nameof(IntervalText));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(SnoozeNextReminderText));
        OnPropertyChanged(nameof(CloseToTrayHintText));
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(DeleteText));
        OnPropertyChanged(nameof(PinWindowText));
        OnPropertyChanged(nameof(ClearText));
        OnPropertyChanged(nameof(CloseText));
        OnPropertyChanged(nameof(SettingsText));
        OnPropertyChanged(nameof(DatabaseLocationText));
        OnPropertyChanged(nameof(LanguageText));
        OnPropertyChanged(nameof(AutoCreateNextRecurringTodosText));
        RefreshCountdowns();
    }
}
