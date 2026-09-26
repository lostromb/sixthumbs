using System.Text.Json;

namespace Sixthumbs.Core;

public sealed class ConfigStore
{
    private readonly string _filePath;

    public ConfigStore(string? filePath = null)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Sixthumbs");
        Directory.CreateDirectory(directory);
        _filePath = filePath ?? Path.Combine(directory, "settings.json");
    }

    public string FilePath => _filePath;

    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonFormat.Options) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, JsonFormat.Options));
    }
}
