using FocusTodo.App.Models;
using FocusTodo.App.Repositories;

namespace FocusTodo.App.Services;

public sealed class TodoService(
    ITodoRepository repository,
    IRecurrenceService recurrenceService,
    ILoggingService loggingService,
    IAppPreferencesService? preferencesService = null) : ITodoService
{
    public Task<List<TodoItem>> GetRootTodosAsync(CancellationToken cancellationToken = default)
    {
        return repository.GetRootTodosAsync(cancellationToken);
    }

    public async Task<TodoItem> CreateRootTodoAsync(string title, DateTime? dueAt, TodoPriority priority, CancellationToken cancellationToken = default)
    {
        var item = NewTodo(null, title, dueAt, priority);
        item.SortOrder = await repository.GetNextSortOrderAsync(null, cancellationToken);
        await repository.AddAsync(item, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<TodoItem> CreateChildTodoAsync(Guid parentId, string title, DateTime? dueAt, TodoPriority priority, CancellationToken cancellationToken = default)
    {
        var parent = await repository.GetByIdAsync(parentId, cancellationToken) ?? throw new InvalidOperationException("Parent todo was not found.");
        var item = NewTodo(parent.Id, title, dueAt, priority);
        item.SortOrder = await repository.GetNextSortOrderAsync(parent.Id, cancellationToken);
        parent.Children.Add(item);
        await repository.AddAsync(item, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task UpdateTodoAsync(
        Guid id,
        string title,
        string description,
        DateTime? dueAt,
        TodoPriority priority,
        RepeatType repeatType,
        int repeatInterval,
        bool reminderEnabled,
        int reminderLeadMinutes,
        int reminderIntervalMinutes,
        int reminderMaxCount,
        CancellationToken cancellationToken = default)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("Todo was not found.");
        item.Title = NormalizeTitle(title);
        item.Description = description.Trim();
        item.DueAt = dueAt;
        item.Priority = priority;
        item.RepeatType = repeatType;
        item.RepeatInterval = Math.Max(1, repeatInterval);
        item.ReminderEnabled = reminderEnabled;
        item.ReminderLeadMinutes = Math.Max(0, reminderLeadMinutes);
        item.ReminderIntervalMinutes = Math.Max(1, reminderIntervalMinutes);
        item.ReminderMaxCount = Math.Max(1, reminderMaxCount);
        item.ReminderSentCount = 0;
        item.LastReminderAt = null;
        item.SnoozedUntil = null;
        item.UpdatedAt = DateTime.Now;
        ResetReminderSchedule(item);
        loggingService.LogInfo($"Todo settings saved. TodoId={item.Id}, Title={item.Title}, DueAt={item.DueAt:O}, ReminderEnabled={item.ReminderEnabled}, NextReminderAt={item.NextReminderAt:O}, MaxCount={item.ReminderMaxCount}, IntervalMinutes={item.ReminderIntervalMinutes}.");
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task ToggleCompletedAsync(Guid id, bool isCompleted, CancellationToken cancellationToken = default)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("Todo was not found.");
        SetCompleted(item, isCompleted);

        if (item.ParentId is not null)
        {
            await UpdateParentCompletionAsync(item.ParentId.Value, cancellationToken);
        }
        else if (isCompleted && item.Children.Count > 0)
        {
            foreach (var child in item.Children)
            {
                SetCompleted(child, true);
            }
        }

        if (isCompleted && preferencesService?.Preferences.AutoCreateNextRecurringTodos == true)
        {
            await CreateNextRecurringInstanceAsync(item, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task SnoozeReminderAsync(Guid id, int minutes, CancellationToken cancellationToken = default)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("Todo was not found.");
        var safeMinutes = Math.Max(1, minutes);
        var snoozedUntil = DateTime.Now.AddMinutes(safeMinutes);
        item.SnoozedUntil = snoozedUntil;
        item.NextReminderAt = snoozedUntil;
        item.UpdatedAt = DateTime.Now;
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTodoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("Todo was not found.");
        await repository.DeleteAsync(item, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateNextRecurringInstanceAsync(TodoItem completedItem, CancellationToken cancellationToken)
    {
        if (completedItem.ParentId is not null || completedItem.RepeatType == RepeatType.None || completedItem.DueAt is null)
        {
            return;
        }

        var nextDueAt = recurrenceService.GetNextOccurrence(completedItem.DueAt, completedItem.RepeatType, completedItem.RepeatInterval);
        if (nextDueAt is null)
        {
            return;
        }

        var sourceId = completedItem.CreatedFromRecurringTaskId ?? completedItem.Id;
        if (await repository.ExistsRecurringInstanceAsync(sourceId, nextDueAt.Value, cancellationToken))
        {
            return;
        }

        var next = NewTodo(null, completedItem.Title, nextDueAt, completedItem.Priority);
        next.Description = completedItem.Description;
        next.SortOrder = await repository.GetNextSortOrderAsync(null, cancellationToken);
        next.RepeatType = completedItem.RepeatType;
        next.RepeatInterval = completedItem.RepeatInterval;
        next.RepeatDays = completedItem.RepeatDays;
        next.ReminderEnabled = completedItem.ReminderEnabled;
        next.ReminderLeadMinutes = completedItem.ReminderLeadMinutes;
        next.ReminderIntervalMinutes = completedItem.ReminderIntervalMinutes;
        next.ReminderMaxCount = completedItem.ReminderMaxCount;
        next.CreatedFromRecurringTaskId = sourceId;
        ResetReminderSchedule(next);

        foreach (var child in completedItem.Children.OrderBy(x => x.SortOrder))
        {
            next.Children.Add(new TodoItem
            {
                ParentId = next.Id,
                Title = child.Title,
                Description = child.Description,
                Priority = child.Priority,
                SortOrder = child.SortOrder,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                ReminderEnabled = child.ReminderEnabled,
                ReminderLeadMinutes = child.ReminderLeadMinutes,
                ReminderIntervalMinutes = child.ReminderIntervalMinutes,
                ReminderMaxCount = child.ReminderMaxCount
            });
        }

        await repository.AddAsync(next, cancellationToken);
    }

    private async Task UpdateParentCompletionAsync(Guid parentId, CancellationToken cancellationToken)
    {
        var parent = await repository.GetByIdAsync(parentId, cancellationToken);
        if (parent is null || parent.Children.Count == 0)
        {
            return;
        }

        var allChildrenDone = parent.Children.All(x => x.IsCompleted);
        if (allChildrenDone && !parent.IsCompleted)
        {
            SetCompleted(parent, true);
        }
        else if (!allChildrenDone && parent.IsCompleted)
        {
            SetCompleted(parent, false);
        }
    }

    private static TodoItem NewTodo(Guid? parentId, string title, DateTime? dueAt, TodoPriority priority)
    {
        var item = new TodoItem
        {
            ParentId = parentId,
            Title = NormalizeTitle(title),
            DueAt = dueAt,
            Priority = priority,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        ResetReminderSchedule(item);
        return item;
    }

    private static string NormalizeTitle(string title)
    {
        var normalized = title.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Title cannot be empty. Type the title in the top new-task input before clicking Add Big Todo.");
        }

        return normalized;
    }

    private static void SetCompleted(TodoItem item, bool isCompleted)
    {
        item.IsCompleted = isCompleted;
        item.CompletedAt = isCompleted ? DateTime.Now : null;
        item.UpdatedAt = DateTime.Now;
        if (isCompleted)
        {
            item.NextReminderAt = null;
            item.SnoozedUntil = null;
        }
        else
        {
            ResetReminderSchedule(item);
        }
    }

    private static void ResetReminderSchedule(TodoItem item)
    {
        if (item.DueAt is null || !item.ReminderEnabled || item.IsCompleted)
        {
            item.NextReminderAt = null;
            return;
        }

        item.NextReminderAt = item.DueAt.Value.AddMinutes(-Math.Max(0, item.ReminderLeadMinutes));
    }
}
