using System.Text.RegularExpressions;
using BiampMatrixController.Models;
using Renci.SshNet;

namespace BiampMatrixController.Services;

public class TesiraService
{
    private readonly object _lock = new();
    private PartyLineConfigService _config;
    private SshClient? _client;
    private ShellStream? _shell;

    private readonly string _host;

    public List<InputPort> Inputs { get; private set; } = [];
    public List<OutputPort> Outputs { get; private set; } = [];

    public TesiraService(
        IConfiguration config,
        PartyLineConfigService partyConfig)
    {
        _host = config["Tesira:Host"] ?? "192.168.1.100";
        _config = partyConfig;
    }

    public async Task InitializeAsync()
    {
        await ConnectAsync();

        await LoadOutputsAsync();
        await LoadInputsAsync();
    }

    private async Task ConnectAsync()
    {
        await Task.Run(() =>
        {
            var kb =
                new KeyboardInteractiveAuthenticationMethod(
                    "default");

            kb.AuthenticationPrompt +=
                (sender, e) =>
                {
                    foreach (var prompt in e.Prompts)
                    {
                        prompt.Response = "";
                    }
                };

            var conn =
                new Renci.SshNet.ConnectionInfo(
                    _host,
                    22,
                    "default",
                    kb);

            _client =
                new SshClient(conn);

            _client.Connect();

            _shell =
                _client.CreateShellStream(
                    "tesira",
                    80,
                    24,
                    800,
                    600,
                    4096);

            Thread.Sleep(1000);

            while (_shell.DataAvailable)
            {
                _shell.Read();
            }
        });
    }

    public async Task<string> ExecuteAsync(string command)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                if (_shell == null)
                    throw new Exception("SSH not connected");

                _shell.WriteLine(command);

                var start = DateTime.UtcNow;

                while ((DateTime.UtcNow - start).TotalSeconds < 5)
                {
                    if (_shell.DataAvailable)
                    {
                        var response = _shell.Read();

                        if (response.Contains("+OK"))
                            return response;
                    }

                    Thread.Sleep(20);
                }

                throw new TimeoutException(command);
            }
        });
    }

    private static string ParseValue(string response)
    {
        var match =
            Regex.Match(
                response,
                "\"value\":(.*)");

        if (!match.Success)
            return "";

        return match.Groups[1]
            .Value
            .Trim()
            .Trim('"');
    }

    private async Task LoadOutputsAsync()
    {
        var response =
            await ExecuteAsync(
                "Mixer1 get numOutputs");

        var count =
            int.Parse(ParseValue(response));

        Outputs.Clear();

        for (int i = 1; i <= count; i++)
        {
            var label =
                ParseValue(
                    await ExecuteAsync(
                        $"Mixer1 get outputLabel {i}"));

            Outputs.Add(new OutputPort
            {
                Number = i,
                Name = label
            });
        }
    }

    private async Task LoadInputsAsync()
    {
        var response =
            await ExecuteAsync(
                "Mixer1 get numInputs");

        var count =
            int.Parse(ParseValue(response));

        Inputs.Clear();

        for (int i = 1; i <= count; i++)
        {
            var label =
                ParseValue(
                    await ExecuteAsync(
                        $"Mixer1 get inputLabel {i}"));

            Inputs.Add(new InputPort
            {
                Number = i,
                Name = label
            });
        }
    }

    public async Task<bool> GetCrosspointAsync(
        int input,
        int output)
    {
        var response =
            await ExecuteAsync(
                $"Mixer1 get crosspointLevelState {input} {output}");

        return ParseValue(response)
            .Equals("true",
                StringComparison.OrdinalIgnoreCase);
    }

    public async Task SetCrosspointAsync(
        int input,
        int output,
        bool connected)
    {
        await ExecuteAsync(
            $"Mixer1 set crosspointLevelState {input} {output} {(connected ? "true" : "false")}");
    }

    public async Task<List<object>> GetOutputStateAsync(
        int output)
    {
        var result = new List<object>();

        foreach (var input in Inputs)
        {
            result.Add(new
            {
                input.Number,
                input.Name,
                Connected =
                    await GetCrosspointAsync(
                        input.Number,
                        output)
            });
        }

        return result;
    }
    public async Task SetInputLabelAsync(
        int input,
        string name)
    {
        await ExecuteAsync(
            $"Mixer1 set inputLabel {input} \"{name}\"");

        var item =
            Inputs.FirstOrDefault(
                x => x.Number == input);

        if (item != null)
        {
            item.Name = name;
        }
    }
    public async Task SetOutputLabelAsync(
        int output,
        string name)
    {
        await ExecuteAsync(
            $"Mixer1 set outputLabel {output} \"{name}\"");

        var item =
            Outputs.FirstOrDefault(
                x => x.Number == output);

        if (item != null)
        {
            item.Name = name;
        }
    }
    public async Task ApplyPartyLine(PartyLine pl)
    {
        foreach (var input in pl.Inputs)
        {
            foreach (var output in pl.Outputs)
            {
                await SetCrosspointAsync(input, output, true);
            }
        }
    }

    public async Task AddInput(int plId, int input)
    {
        var pl = _config.PartyLines.First(x => x.Id == plId);

        if (!pl.Inputs.Contains(input))
            pl.Inputs.Add(input);

        _config.Save();

        await ApplyPartyLine(pl);
    }
    public async Task AddOutput(int plId, int output)
{
    var pl = _config.PartyLines.First(x => x.Id == plId);

    if (!pl.Outputs.Contains(output))
        pl.Outputs.Add(output);

    _config.Save();

    await ApplyPartyLine(pl);
}
    public async Task RemoveInput(int plId, int input)
    {
        var pl = _config.PartyLines.FirstOrDefault(x => x.Id == plId);

        if (pl == null)
            throw new Exception($"PartyLine {plId} not found");

        if (pl.Inputs.Contains(input))
            pl.Inputs.Remove(input);

        // TURN OFF ALL CROSSPOINTS for this input in this PL
        foreach (var output in pl.Outputs)
        {
            await SetCrosspointAsync(input, output, false);
        }

        _config.Save();
    }
    public async Task RemoveOutput(int plId, int output)
    {
        var pl = _config.PartyLines.FirstOrDefault(x => x.Id == plId);

        if (pl == null)
            throw new Exception($"PartyLine {plId} not found");

        if (pl.Outputs.Contains(output))
            pl.Outputs.Remove(output);

        // TURN OFF ALL CROSSPOINTS for this output in this PL
        foreach (var input in pl.Inputs)
        {
            await SetCrosspointAsync(input, output, false);
        }

        _config.Save();
    }
}