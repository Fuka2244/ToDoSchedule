using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusTodo.App.Data;
using FocusTodo.App.Models;
using FocusTodo.App.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FocusTodo.App.ViewModels;

public sealed partial class PinnedTodoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICountdownService _countdownService;
    private readonly IPinnedWindowService _pinnedWindowService;
    private readonly ILoggingService _loggingService;
    private readonly ILocalizationService? _localizationService;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };

    public PinnedTodoViewModel(
        TodoItem todo,
        PinnedWindowSetting setting,
        IServiceScopeFactory scopeFactory,
        ICountdownService countdownService,
        IPinnedWindowService pinnedWindowService,
        ILoggingService loggingService,
        ILocalizationService? localizationService = null)
    {
        TodoId = todo.Id;
        Setting = setting;
        _scopeFactory = scopeFactory;
        _countdownService = countdownService;
        _pinnedWindowService = pinnedWindowService;
        _loggingService = loggingService;
        _localizationService = localizationService;
        ApplyTodo(todo);
        Opacity = setting.Opacity;
        IsTopMost = setting.IsTopMost;
        IsLocked = setting.IsLocked;
        ShowCompleted = setting.ShowCompleted;
        _timer.Tick += (_, _) => RefreshCountdown();
        _timer.Start();
        if (_localizationService is not null)
        {
            _localizationService.LanguageChanged += (_, _) => RefreshLocalizedText();
        }
    }

    public Guid TodoId { get; }
    public PinnedWindowSetting Setting { get; }
    public ObservableCollection<PinnedChildTodoViewModel> Children { get; } = [];
    public string TopText => T("Top");
    public string LockText => T("Lock");
    public string ShowDoneText => T("ShowDone");
    public string OpacityText => T("Opacity");
    public string AddText => T("Add");
    public string NewSmallTodoText => T("NewSmallTodo");
    public string UnpinText => T("Unpin");

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string countdownText = string.Empty;

    [ObservableProperty]
    private string dueText = string.Empty;

    [ObservableProperty]
    private string newChildTitle = string.Empty;

    [ObservableProperty]
    private double opacity;

    [ObservableProperty]
    private bool isTopMost;

    [ObservableProperty]
    private bool isLocked;

    [ObservableProperty]
    private bool showCompleted;

    partial void OnOpacityChanged(double value)
    {
        Setting.Opacity = Math.Clamp(value, 0.35, 1.0);
        _ = SaveSettingAsync();
    }

    partial void OnIsTopMostChanged(bool value)
    {
        Setting.IsTopMost = value;
        _ = SaveSettingAsync();
    }

    partial void OnIsLockedChanged(bool value)
    {
        Setting.IsLocked = value;
        _ = SaveSettingAsync();
    }

    partial void OnShowCompletedChanged(bool value)
    {
        Setting.ShowCompleted = value;
        _ = SaveSettingAsync();
        _ = ReloadAsync();
    }

    [RelayCommand]
    public async Task ReloadAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FocusTodoDbContext>();
            var todo = await dbContext.TodoItems
                .AsNoTracking()
                .Include(x => x.Children.OrderBy(c => c.SortOrder))
                .FirstOrDefaultAsync(x => x.Id == TodoId);
            if (todo is not null)
            {
                ApplyTodo(todo);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Reload pinned window failed");
        }
    }

    [RelayCommand]
    public async Task AddChildAsync()
    {
        if (string.IsNullOrWhiteSpace(NewChildTitle))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var todoService = scope.ServiceProvider.GetRequiredService<ITodoService>();
            await todoService.CreateChildTodoAsync(TodoId, NewChildTitle.Trim(), null, TodoPriority.Normal);
            NewChildTitle = string.Empty;
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Add child from pinned window failed");
        }
    }

    [RelayCommand]
    public async Task ToggleChildAsync(PinnedChildTodoViewModel? child)
    {
        if (child is null)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var todoService = scope.ServiceProvider.GetRequiredService<ITodoService>();
            await todoService.ToggleCompletedAsync(child.Id, child.IsCompleted);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Toggle child from pinned window failed");
        }
    }

    [RelayCommand]
    public async Task UnpinAsync()
    {
        await _pinnedWindowService.UnpinAsync(TodoId);
    }

    public void SetWindowBounds(double left, double top, double width, double height)
    {
        Setting.Left = left;
        Setting.Top = top;
        Setting.Width = width;
        Setting.Height = height;
        _ = SaveSettingAsync();
    }

    private async Task SaveSettingAsync()
    {
        try
        {
            await _pinnedWindowService.SaveSettingAsync(Setting);
        }
        catch (Exception ex)
        {
            _loggingService.LogError(ex, "Save pinned window setting failed");
        }
    }

    private void ApplyTodo(TodoItem todo)
    {
        Title = todo.Title;
        DueText = todo.DueAt?.ToString("yyyy-MM-dd HH:mm") ?? T("NoDdl");
        CountdownText = _countdownService.GetCountdownText(todo, DateTime.Now);
        Children.Clear();
        foreach (var child in todo.Children.Where(x => ShowCompleted || !x.IsCompleted).OrderBy(x => x.SortOrder))
        {
            Children.Add(new PinnedChildTodoViewModel(child));
        }
    }

    private void RefreshCountdown()
    {
        _ = ReloadAsync();
    }

    private string T(string key)
    {
        return _localizationService?.Get(key) ?? key switch
        {
            "Top" => "Top",
            "Lock" => "Lock",
            "ShowDone" => "Show done",
            "Opacity" => "Opacity",
            "Add" => "Add",
            "NewSmallTodo" => "New small todo",
            "Unpin" => "Unpin",
            "NoDdl" => "No DDL",
            _ => key
        };
    }

    private void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(TopText));
        OnPropertyChanged(nameof(LockText));
        OnPropertyChanged(nameof(ShowDoneText));
        OnPropertyChanged(nameof(OpacityText));
        OnPropertyChanged(nameof(AddText));
        OnPropertyChanged(nameof(NewSmallTodoText));
        OnPropertyChanged(nameof(UnpinText));
        _ = ReloadAsync();
    }
}
