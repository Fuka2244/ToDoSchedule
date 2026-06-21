using System.Windows;
using System.Windows.Data;
using FocusTodo.App.ViewModels;

namespace FocusTodo.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (System.Windows.Application.Current.Properties["ExitRequested"] is true)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void CommitTextInputsBeforeCommand(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        CommitTextBoxes(this);
    }

    private async void AddRootTodoButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.AddRootTodoAsync(NewTodoTitleBox.Text);
        }
    }

    private async void AddChildTodoButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.AddChildTodoAsync(NewTodoTitleBox.Text);
        }
    }

    private static void CommitTextBoxes(DependencyObject parent)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is System.Windows.Controls.TextBox textBox)
            {
                BindingOperations.GetBindingExpression(textBox, System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
            }

            CommitTextBoxes(child);
        }
    }
}
