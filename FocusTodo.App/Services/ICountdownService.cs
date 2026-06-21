using FocusTodo.App.Models;

namespace FocusTodo.App.Services;

public interface ICountdownService
{
    string GetCountdownText(TodoItem item, DateTime now);
    string GetDueState(TodoItem item, DateTime now);
}
