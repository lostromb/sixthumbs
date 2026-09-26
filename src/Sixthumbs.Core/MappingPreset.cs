using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sixthumbs.Core;

public static class JsonFormat
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed class MappingPreset
{
    public int Version { get; set; } = 1;
    public SourceMapping Mapping { get; set; } = new();

    public static MappingPreset From(SourceMapping mapping) =>
        new()
        {
            Version = 1,
            Mapping = mapping.CloneBindings(),
        };

    public void ApplyTo(SourceMapping target) => target.ReplaceBindings(Mapping);
}

public static class MappingPresetStore
{
    public static string Write(MappingPreset preset) =>
        JsonSerializer.Serialize(preset, JsonFormat.Options);

    public static MappingPreset Read(string json)
    {
        var preset = JsonSerializer.Deserialize<MappingPreset>(json, JsonFormat.Options)
                     ?? throw new InvalidDataException("Preset file was empty.");
        if (preset.Mapping is null)
        {
            throw new InvalidDataException("Preset file has no mapping.");
        }

        return preset;
    }

    public static void SaveFile(string path, SourceMapping mapping)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Write(MappingPreset.From(mapping)));
    }

    public static void LoadFile(string path, SourceMapping target)
    {
        var json = File.ReadAllText(path);
        Read(json).ApplyTo(target);
    }
}
