namespace Sixthumbs.Core;

public enum AppRole
{
    Host = 0,
    Client = 1,
}

public sealed class AppSettings
{
    public AppRole Role { get; set; } = AppRole.Host;
    public int ListenPort { get; set; } = 26180;
    public string Password { get; set; } = "";
    public string HostAddress { get; set; } = "127.0.0.1";
    public bool ListenEnabled { get; set; }
    public Dictionary<string, SourceMapping> Mappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public SourceMapping MappingFor(string sourceId)
    {
        if (!Mappings.TryGetValue(sourceId, out var mapping))
        {
            mapping = new SourceMapping();
            Mappings[sourceId] = mapping;
        }

        foreach (var control in Xbox360Controls.All)
        {
            mapping.GetOrCreate(control);
        }

        return mapping;
    }
}
