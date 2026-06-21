using System.Text.Json;

public class PartyLineConfigService
{
    private readonly string _filePath = "appsettings.json";
    private readonly object _lock = new();

    public List<PartyLine> PartyLines { get; private set; } = new();

    public PartyLineConfigService()
    {
        Load();
    }

    public void Load()
    {
        var json = File.ReadAllText(_filePath);

        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("PartyLines", out var pl))
        {
            PartyLines =
                JsonSerializer.Deserialize<List<PartyLine>>(
                    pl.GetRawText()) ?? new();
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            var json = File.ReadAllText(_filePath);

            var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;

            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;

            dict["PartyLines"] = PartyLines;

            var updated =
                JsonSerializer.Serialize(dict, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            File.WriteAllText(_filePath, updated);
        }
    }
}