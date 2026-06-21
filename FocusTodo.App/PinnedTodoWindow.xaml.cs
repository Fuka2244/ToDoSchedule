using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using FocusTodo.App.ViewModels;

namespace FocusTodo.App;

public partial class PinnedTodoWindow : Window
{
    public PinnedTodoWindow(PinnedTodoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => ApplyInitialBounds(viewModel);
        LocationChanged += (_, _) => SaveBounds();
        SizeChanged += (_, _) => SaveBounds();
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    public void ForceClose()
    {
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SaveBounds();
        base.OnClosing(e);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PinnedTodoViewModel { IsLocked: false } && e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PinnedTodoViewModel.IsLocked) && DataContext is PinnedTodoViewModel viewModel)
        {
            ResizeMode = viewModel.IsLocked ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
        }
    }

    private void ApplyInitialBounds(PinnedTodoViewModel viewModel)
    {
        Left = viewModel.Setting.Left;
        Top = viewModel.Setting.Top;
        Width = Math.Max(MinWidth, viewModel.Setting.Width);
        Height = Math.Max(MinHeight, viewModel.Setting.Height);
        ResizeMode = viewModel.IsLocked ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
        EnsureVisibleOnAnyScreen();
        SaveBounds();
    }

    private void SaveBounds()
    {
        if (DataContext is PinnedTodoViewModel viewModel)
        {
            viewModel.SetWindowBounds(Left, Top, Width, Height);
        }
    }

    private void EnsureVisibleOnAnyScreen()
    {
        var left = SystemParameters.VirtualScreenLeft;
        var top = SystemParameters.VirtualScreenTop;
        var right = left + SystemParameters.VirtualScreenWidth;
        var bottom = top + SystemParameters.VirtualScreenHeight;

        if (Left + 80 < left || Top + 80 < top || Left > right - 80 || Top > bottom - 80)
        {
            Left = Math.Max(left + 20, Math.Min(left + 80, right - Width - 20));
            Top = Math.Max(top + 20, Math.Min(top + 80, bottom - Height - 20));
        }
    }
}
