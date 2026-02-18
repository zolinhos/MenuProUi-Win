using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using MenuProUI.Models;

namespace MenuProUI.Dialogs;

public partial class SettingsDialog : Window
{
    public AppPreferences Result { get; }

    public SettingsDialog() : this(new AppPreferences())
    {
    }

    public SettingsDialog(AppPreferences current)
    {
        InitializeComponent();
        Result = new AppPreferences
        {
            Theme = current.Theme,
            ClientsIcon = current.ClientsIcon,
            AccessesIcon = current.AccessesIcon,
            CompactAccessRows = current.CompactAccessRows,
            ConnectivityTimeoutMs = current.ConnectivityTimeoutMs,
            ConnectivityMaxConcurrency = current.ConnectivityMaxConcurrency,
            UrlFallbackPortsCsv = current.UrlFallbackPortsCsv,
            RdpForceFullscreen = current.RdpForceFullscreen
        };

        ConnectivityTimeoutBox.Text = Result.ConnectivityTimeoutMs.ToString();
        ConnectivityConcurrencyBox.Text = Result.ConnectivityMaxConcurrency.ToString();
        UrlFallbackPortsBox.Text = Result.UrlFallbackPortsCsv;
        RdpForceFullscreenBox.IsChecked = Result.RdpForceFullscreen;
    }

    private async void OnSave(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var timeout = ParseInt(ConnectivityTimeoutBox.Text, 3000, 500, 60000);
        var concurrency = ParseInt(ConnectivityConcurrencyBox.Text, 24, 1, 128);
        var portsCsv = NormalizePortsCsv(UrlFallbackPortsBox.Text);
        if (string.IsNullOrWhiteSpace(portsCsv))
        {
            await new ConfirmDialog("Informe ao menos uma porta fallback válida.", "Validação")
                .ShowDialog<bool>(this);
            return;
        }

        Result.ConnectivityTimeoutMs = timeout;
        Result.ConnectivityMaxConcurrency = concurrency;
        Result.UrlFallbackPortsCsv = portsCsv;
        Result.RdpForceFullscreen = RdpForceFullscreenBox.IsChecked == true;
        Close(true);
    }

    private void OnCancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);

    private static int ParseInt(string? raw, int fallback, int min, int max)
    {
        if (!int.TryParse((raw ?? string.Empty).Trim(), out var value))
            return fallback;
        return Math.Clamp(value, min, max);
    }

    private static string NormalizePortsCsv(string? raw)
    {
        var ports = new List<int>();
        foreach (var part in (raw ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(part.Trim(), out var p)) continue;
            if (p is < 1 or > 65535) continue;
            if (!ports.Contains(p)) ports.Add(p);
        }

        return string.Join(",", ports.Select(p => p.ToString()));
    }
}
