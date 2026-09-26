using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sixthumbs.Core;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _filePath;

    public ConfigStore(string? filePath = null)
    {
        var directory = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Sixthumbs");
        Directory.CreateDirectory(directory);
        _filePath = filePath ?? System.IO.Path.Combine(directory, "settings.json");
    }

    public string FilePath => _filePath;

    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
