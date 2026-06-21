using FocusTodo.App.Data;
using FocusTodo.App.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusTodo.App.Repositories;

public sealed class TodoRepository(FocusTodoDbContext dbContext) : ITodoRepository
{
    public async Task<List<TodoItem>> GetRootTodosAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.TodoItems
            .AsNoTracking()
            .Include(x => x.Children.OrderBy(c => c.SortOrder))
            .Where(x => x.ParentId == null)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.TodoItems
            .Include(x => x.Children.OrderBy(c => c.SortOrder))
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<List<TodoItem>> GetDueReminderTodosAsync(DateTime now, int take, CancellationToken cancellationToken = default)
    {
        return await dbContext.TodoItems
            .Where(x => x.ReminderEnabled
                && !x.IsCompleted
                && x.NextReminderAt != null
                && x.NextReminderAt <= now
                && (x.SnoozedUntil == null || x.SnoozedUntil <= now)
                && x.ReminderSentCount < x.ReminderMaxCount)
            .OrderBy(x => x.NextReminderAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsRecurringInstanceAsync(Guid sourceTodoId, DateTime dueAt, CancellationToken cancellationToken = default)
    {
        return await dbContext.TodoItems.AnyAsync(
            x => x.CreatedFromRecurringTaskId == sourceTodoId && x.DueAt == dueAt,
            cancellationToken);
    }

    public async Task<int> GetNextSortOrderAsync(Guid? parentId, CancellationToken cancellationToken = default)
    {
        var orders = dbContext.TodoItems.Where(x => x.ParentId == parentId).Select(x => (int?)x.SortOrder);
        return (await orders.MaxAsync(cancellationToken) ?? 0) + 1;
    }

    public async Task AddAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        await dbContext.TodoItems.AddAsync(item, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        dbContext.TodoItems.Remove(item);
        return Task.CompletedTask;
    }
}
