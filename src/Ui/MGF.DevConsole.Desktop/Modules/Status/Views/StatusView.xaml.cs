namespace MGF.DevConsole.Desktop.Modules.Status.Views;

using System.Windows;
using System.Windows.Controls;
using MGF.DevConsole.Desktop.Modules.Status.ViewModels;

public partial class StatusView : UserControl
{
    public StatusView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is StatusViewModel viewModel)
        {
            viewModel.Start();
        }
    }
}
