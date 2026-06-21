namespace FocusTodo.App.Models;

public sealed class PinnedWindowSetting
{
    public int Id { get; set; }
    public Guid TodoItemId { get; set; }
    public TodoItem? TodoItem { get; set; }
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; } = 320;
    public double Height { get; set; } = 420;
    public double Opacity { get; set; } = 0.95;
    public bool IsTopMost { get; set; } = true;
    public bool IsLocked { get; set; }
    public bool ShowCompleted { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
