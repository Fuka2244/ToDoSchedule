namespace FocusTodo.App.Models;

public sealed class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ParentId { get; set; }
    public TodoItem? Parent { get; set; }
    public List<TodoItem> Children { get; set; } = [];
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TodoPriority Priority { get; set; } = TodoPriority.Normal;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? DueAt { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int SortOrder { get; set; }
    public bool IsPinned { get; set; }
    public RepeatType RepeatType { get; set; } = RepeatType.None;
    public int RepeatInterval { get; set; } = 1;
    public string? RepeatDays { get; set; }
    public bool ReminderEnabled { get; set; }
    public int ReminderLeadMinutes { get; set; } = 20;
    public int ReminderIntervalMinutes { get; set; } = 30;
    public int ReminderMaxCount { get; set; } = 3;
    public int ReminderSentCount { get; set; }
    public DateTime? LastReminderAt { get; set; }
    public DateTime? NextReminderAt { get; set; }
    public DateTime? SnoozedUntil { get; set; }
    public Guid? CreatedFromRecurringTaskId { get; set; }
}
