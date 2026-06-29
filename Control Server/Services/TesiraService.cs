using System.Diagnostics;
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
    public string Applystatus = "";
    private readonly string _host;

    public List<InputPort> Inputs { get; private set; } = [];
    public List<OutputPort> Outputs { get; private set; } = [];


    public TesiraService(
        IConfiguration config,
        PartyLineConfigService partyConfig)
    {
        _host = config["Tesira:Host"] ?? "10.10.10.01";
        _config = partyConfig;
    }

    public async Task InitializeAsync()
    {
        Console.WriteLine("Connecting To DSP..."+ _host);
        await ConnectAsync();
        Console.WriteLine("Syncing Form DSP...");
        await LoadOutputsAsync();
        await LoadInputsAsync();
        Console.WriteLine("Synced From DSP Compleate.");
    }
    private async Task ApplyForcedCrosspoints()
    {
        foreach (var cp in _config.ForcedCrosspoints)
        {
            switch (cp.State)
            {
                case ForcedState.Default:
                    // Do nothing. PartyLines determine the state.
                    break;

                case ForcedState.ForceOn:
                    
                    await SetCrosspointAsync(cp.Input, cp.Output, GetFinalCrosspointState(cp.Input, cp.Output));
                    break;

                case ForcedState.ForceOff:
                    await SetCrosspointAsync(cp.Input, cp.Output, GetFinalCrosspointState(cp.Input, cp.Output));
                    break;
            }
        }
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
    private async Task ReconnectAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                _shell?.Dispose();
                _client?.Dispose();
            }
            catch { }

            _shell = null;
            _client = null;

            ConnectAsync().Wait();
        });
    }
    private bool IsConnected()
    {
        return _client != null && _client.IsConnected;
    }


    public async Task<string> ExecuteAsync(string command)
    {
        return await Task.Run(async () =>
        {
            lock (_lock)
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        if (!IsConnected())
                            ReconnectAsync().Wait();

                        _shell!.WriteLine(command);
                       // Console.WriteLine("Running Command: "+ command);

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
                    catch
                    {
                        // force reconnect and retry once
                        ReconnectAsync().Wait();
                    }
                }

                throw new Exception($"Command failed after reconnect: {command}");
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

        foreach (var output in pl.Outputs)
        {
            await ReevaluateCrosspoint(input, output);
        }
    }
    public async Task RebuildMatrix()
    {
        // Clear everything

        foreach (var input in Inputs)
        {
            Applystatus = "Applying Changes to Port:" + input;
            foreach (var output in Outputs)
            {
                await SetCrosspointAsync(
                    input.Number,
                    output.Number,
                    false);
            }
        }

        // Apply PartyLines

        foreach (var pl in _config.PartyLines)
        {
            await ApplyPartyLine(pl);
        }

        // Forced routes always win

        await ApplyForcedCrosspoints();
    }
    public async Task AddOutput(int plId, int output)
    {
        var pl = _config.PartyLines.First(x => x.Id == plId);

        if (!pl.Outputs.Contains(output))
            pl.Outputs.Add(output);

        _config.Save();

        foreach (var input in pl.Inputs)
        {
            await ReevaluateCrosspoint(input, output);
        }
    }
    public async Task RemoveInput(int plId, int input)
    {
        var pl = _config.PartyLines.First(x => x.Id == plId);

        // Only proceed if it actually existed
        if (!pl.Inputs.Contains(input))
            return;

        pl.Inputs.Remove(input);

        _config.Save();

        var changes = new List<(int input, int output, bool state)>();

        foreach (var output in pl.Outputs)
        {
            await ReevaluateCrosspoint(input, output);
        }
    }

    public async Task ClearForcedCrosspoint(int input, int output)
    {
        var cp = _config.ForcedCrosspoints
            .FirstOrDefault(x =>
                x.Input == input &&
                x.Output == output);

        if (cp != null)
        {
            _config.ForcedCrosspoints.Remove(cp);
            _config.Save();
        }

        // Recompute what THIS ONE crosspoint should be
        bool shouldBeOn = IsCrosspointRequiredByPartyLines(input, output);

        await SetCrosspointAsync(input, output, GetFinalCrosspointState(input, output));
        
    }
    private bool IsCrosspointRequiredByPartyLines(int input, int output)
    {
        foreach (var pl in _config.PartyLines)
        {
            if (pl.Inputs.Contains(input) &&
                pl.Outputs.Contains(output))
            {
                return true;
            }
        }

        return false;
    }
    public async Task RemoveOutput(int plId, int output)
    {
        var pl = _config.PartyLines.First(x => x.Id == plId);

        pl.Outputs.Remove(output);

        _config.Save();

        var changes = new List<(int input, int output, bool state)>();

        foreach (var input in pl.Inputs)
        {
            await ReevaluateCrosspoint(input, output);
        }
    }
    public async Task ForceCrosspoint(
        int input,
        int output,
        ForcedState state)
    {
        var cp = _config.ForcedCrosspoints
            .FirstOrDefault(x =>
                x.Input == input &&
                x.Output == output);

        var previousState = cp?.State ?? ForcedState.Default;

        if (cp == null)
        {
            cp = new ForcedCrosspoint
            {
                Input = input,
                Output = output,
                State = state
            };

            _config.ForcedCrosspoints.Add(cp);
        }
        else
        {
            cp.State = state;
        }

        _config.Save();

        // =========================
        // FAST APPLY LOGIC
        // =========================

        bool? desiredState = null;

        switch (state)
        {
            case ForcedState.Default:
                desiredState = IsCrosspointRequiredByPartyLines(input, output);
                break;

            case ForcedState.ForceOn:
                desiredState = true;
                break;

            case ForcedState.ForceOff:
                desiredState = false;
                break;
        }
        await SetCrosspointAsync(input, output, GetFinalCrosspointState(input, output));
        
    }
    private bool IsValidRoute(int input, int output)
    {
        return Inputs.Any(i => i.Number == input)
            && Outputs.Any(o => o.Number == output);
    }

    private bool GetFinalCrosspointState(int input, int output)
    {
        if (!IsValidRoute(input, output))
            return false;

        var forced = _config.ForcedCrosspoints
            .FirstOrDefault(x => x.Input == input && x.Output == output);

        if (forced != null)
        {
            if (forced.State == ForcedState.ForceOn)
                return true;

            if (forced.State == ForcedState.ForceOff)
                return false;
        }

        foreach (var pl in _config.PartyLines)
        {
            if (pl.Inputs.Contains(input) &&
                pl.Outputs.Contains(output))
            {
                return true;
            }
        }

        return false;
    }

    public async Task RenamePartyLine(int plId, string newName)
    {
        var pl = _config.PartyLines.FirstOrDefault(x => x.Id == plId)
            ?? throw new Exception($"PartyLine {plId} not found");

        pl.Name = newName;

        _config.Save();
    }
    private async Task ReevaluateCrosspoint(int input, int output)
    {
        await SetCrosspointAsync(
            input,
            output,
            GetFinalCrosspointState(input, output));
    }
    public async Task DeletePartyLine(int plId)
    {
        var pl = _config.PartyLines.FirstOrDefault(x => x.Id == plId)
            ?? throw new Exception($"PartyLine {plId} not found");

        _config.PartyLines.Remove(pl);

        _config.Save();

        // IMPORTANT: rebuild DSP state after removal
        await RebuildMatrix();
    }
}