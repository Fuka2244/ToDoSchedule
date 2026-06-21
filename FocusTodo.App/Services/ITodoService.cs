using FocusTodo.App.Models;

namespace FocusTodo.App.Services;

public interface ITodoService
{
    Task<List<TodoItem>> GetRootTodosAsync(CancellationToken cancellationToken = default);
    Task<TodoItem> CreateRootTodoAsync(string title, DateTime? dueAt, TodoPriority priority, CancellationToken cancellationToken = default);
    Task<TodoItem> CreateChildTodoAsync(Guid parentId, string title, DateTime? dueAt, TodoPriority priority, CancellationToken cancellationToken = default);
    Task UpdateTodoAsync(
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
        CancellationToken cancellationToken = default);
    Task ToggleCompletedAsync(Guid id, bool isCompleted, CancellationToken cancellationToken = default);
    Task SnoozeReminderAsync(Guid id, int minutes, CancellationToken cancellationToken = default);
    Task DeleteTodoAsync(Guid id, CancellationToken cancellationToken = default);
}
