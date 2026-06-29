using BiampMatrixController.Models;
using System.Text.Json;

public class PartyLineConfigService
{
    private readonly string _filePath = "matrixstatus.json";
    private readonly object _lock = new();
    private FileSystemWatcher _watcher;
    public MatrixConfig Config { get; private set; } = new();

    public List<PartyLine> PartyLines => Config.PartyLines;
    public List<ForcedCrosspoint> ForcedCrosspoints => Config.ForcedCrosspoints;

    public PartyLineConfigService()
    {
        Load();

        var fullPath = Path.GetFullPath(_filePath);
        var directory = Path.GetDirectoryName(fullPath)!;

        _watcher = new FileSystemWatcher(directory)
        {
            Filter = Path.GetFileName(fullPath),
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _watcher.Changed += (s, e) =>
        {
            try
            {
                Thread.Sleep(100);
                Load();
            }
            catch
            {
                // avoid crash loops if file is mid-write
            }
        };
    }


    // =========================
    // LOAD
    // =========================
    public void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                Config = new MatrixConfig();
                return;
            }

            var json = File.ReadAllText(_filePath);

            var config =
                JsonSerializer.Deserialize<MatrixConfig>(json);

            if (config != null)
            {
                Config = config;
            }

            // safety: never null lists
            Config.PartyLines ??= new List<PartyLine>();
            Config.ForcedCrosspoints ??= new List<ForcedCrosspoint>();
           // Console.WriteLine("config reloaded");
        }
        catch
        {
            // if file is corrupted, fall back safely
            Config = new MatrixConfig();
        }
    }

    // =========================
    // SAVE (FAST + SAFE)
    // =========================
    public void Save()
    {
        lock (_lock)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json =
                JsonSerializer.Serialize(Config, options);

            File.WriteAllText(_filePath, json);
        }

        Load();
    }

    // =========================
    // OPTIONAL HELPERS
    // =========================

    public PartyLine? GetPartyLine(int id)
    {
        return Config.PartyLines.FirstOrDefault(x => x.Id == id);
    }

    public ForcedCrosspoint? GetForcedCrosspoint(int input, int output)
    {
        return Config.ForcedCrosspoints.FirstOrDefault(x =>
            x.Input == input && x.Output == output);
    }
}