using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusTodo.App.Models;
using FocusTodo.App.Services;

namespace FocusTodo.App.ViewModels;

public sealed partial class TodoItemViewModel : ObservableObject
{
    private readonly ICountdownService _countdownService;

    public TodoItemViewModel(TodoItem model, ICountdownService countdownService)
    {
        Model = model;
        _countdownService = countdownService;
        Children = new ObservableCollection<TodoItemViewModel>(model.Children.OrderBy(x => x.SortOrder).Select(x => new TodoItemViewModel(x, countdownService)));
        RefreshCountdown(DateTime.Now);
    }

    public TodoItem Model { get; }
    public ObservableCollection<TodoItemViewModel> Children { get; }
    public Guid Id => Model.Id;
    public bool IsRoot => Model.ParentId is null;
    public string PriorityText => Model.Priority.ToString();
    public string DueText => Model.DueAt?.ToString("yyyy-MM-dd HH:mm") ?? "No DDL";

    [ObservableProperty]
    private bool isExpanded = true;

    [ObservableProperty]
    private string countdownText = string.Empty;

    [ObservableProperty]
    private string dueState = "Normal";

    public string Title
    {
        get => Model.Title;
        set
        {
            if (Model.Title != value)
            {
                Model.Title = value;
                OnPropertyChanged();
            }
        }
    }

    public string Description
    {
        get => Model.Description;
        set
        {
            if (Model.Description != value)
            {
                Model.Description = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsCompleted
    {
        get => Model.IsCompleted;
        set
        {
            if (Model.IsCompleted != value)
            {
                Model.IsCompleted = value;
                OnPropertyChanged();
            }
        }
    }

    public DateTime? DueAt
    {
        get => Model.DueAt;
        set
        {
            if (Model.DueAt != value)
            {
                Model.DueAt = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DueText));
            }
        }
    }

    public TodoPriority Priority
    {
        get => Model.Priority;
        set
        {
            if (Model.Priority != value)
            {
                Model.Priority = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PriorityText));
            }
        }
    }

    public void RefreshCountdown(DateTime now)
    {
        CountdownText = _countdownService.GetCountdownText(Model, now);
        DueState = _countdownService.GetDueState(Model, now);
        foreach (var child in Children)
        {
            child.RefreshCountdown(now);
        }
    }
}
