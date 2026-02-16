using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using MenuProUI.Services;

namespace MenuProUI;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception ex)
                WriteCrashLog(ex);
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            WriteCrashLog(eventArgs.Exception);
            eventArgs.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            var dir = AppPaths.AppDir;
            var file = Path.Combine(dir, "startup-crash.log");
            var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n";
            File.AppendAllText(file, text);
        }
        catch
        {
            try
            {
                var file = Path.Combine(Path.GetTempPath(), "MenuProUI-startup-crash.log");
                var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n";
                File.AppendAllText(file, text);
            }
            catch
            {
                // sem fallback adicional
            }
        }
    }
}
