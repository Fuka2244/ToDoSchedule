using CommunityToolkit.Mvvm.ComponentModel;
using FocusTodo.App.Models;

namespace FocusTodo.App.ViewModels;

public sealed partial class PinnedChildTodoViewModel(TodoItem model) : ObservableObject
{
    public Guid Id => model.Id;
    public string Title => model.Title;

    [ObservableProperty]
    private bool isCompleted = model.IsCompleted;
}
