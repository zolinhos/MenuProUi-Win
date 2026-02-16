using Avalonia.Controls;

namespace MenuProUI.Dialogs;

public enum ConnectivityScopeMode
{
    Cancel = 0,
    SelectedClient = 1,
    AllClients = 2
}

public partial class ConnectivityScopeDialog : Window
{
    public ConnectivityScopeDialog()
    {
        InitializeComponent();
    }

    private void OnSelectedClient(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close(ConnectivityScopeMode.SelectedClient);

    private void OnAllClients(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close(ConnectivityScopeMode.AllClients);

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Close(ConnectivityScopeMode.Cancel);
}
