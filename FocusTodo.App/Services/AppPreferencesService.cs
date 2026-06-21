using System.IO;
using System.Text.Json;

namespace FocusTodo.App.Services;

public sealed class AppPreferencesService : IAppPreferencesService
{
    private const string FileName = "preferences.json";
    private readonly string _path;

    public AppPreferencesService()
    {
        ConfigDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusTodo");
        Directory.CreateDirectory(ConfigDirectory);
        _path = Path.Combine(ConfigDirectory, FileName);
        Preferences = Load();
    }

    public string ConfigDirectory { get; }
    public AppPreferences Preferences { get; }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(Preferences, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_path, json, cancellationToken);
    }

    private AppPreferences Load()
    {
        var defaults = new AppPreferences
        {
            DbDirectory = ConfigDirectory,
            Language = "zh-CN",
            AutoCreateNextRecurringTodos = false
        };

        if (!File.Exists(_path))
        {
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var loaded = JsonSerializer.Deserialize<AppPreferences>(json) ?? defaults;
            loaded.DbDirectory = string.IsNullOrWhiteSpace(loaded.DbDirectory) ? ConfigDirectory : loaded.DbDirectory;
            loaded.Language = NormalizeLanguage(loaded.Language);
            return loaded;
        }
        catch
        {
            return defaults;
        }
    }

    private static string NormalizeLanguage(string? language)
    {
        return string.Equals(language, "en-US", StringComparison.OrdinalIgnoreCase) ? "en-US" : "zh-CN";
    }
}
