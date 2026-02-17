using System;
using System.IO;
using System.Text.Json;
using MenuProUI.Models;

namespace MenuProUI.Services;

public class AppPreferencesService
{
    private static string PreferencesPath => Path.Combine(AppPaths.AppDir, "preferences.json");

    public AppPreferences Load()
    {
        try
        {
            if (!File.Exists(PreferencesPath))
                return new AppPreferences();

            var json = File.ReadAllText(PreferencesPath);
            return JsonSerializer.Deserialize<AppPreferences>(json) ?? new AppPreferences();
        }
        catch
        {
            return new AppPreferences();
        }
    }

    public void Save(AppPreferences preferences)
    {
        try
        {
            var json = JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PreferencesPath, json);
        }
        catch
        {
        }
    }
}
