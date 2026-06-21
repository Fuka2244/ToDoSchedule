using FocusTodo.App.Models;

namespace FocusTodo.App.Repositories;

public interface ITodoRepository
{
    Task<List<TodoItem>> GetRootTodosAsync(CancellationToken cancellationToken = default);
    Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<TodoItem>> GetDueReminderTodosAsync(DateTime now, int take, CancellationToken cancellationToken = default);
    Task<bool> ExistsRecurringInstanceAsync(Guid sourceTodoId, DateTime dueAt, CancellationToken cancellationToken = default);
    Task<int> GetNextSortOrderAsync(Guid? parentId, CancellationToken cancellationToken = default);
    Task AddAsync(TodoItem item, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(TodoItem item, CancellationToken cancellationToken = default);
}
