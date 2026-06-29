using BiampMatrixController.Models;
using BiampMatrixController.Services;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter(
    "Microsoft.AspNetCore.Hosting.Diagnostics",
    LogLevel.Warning);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter()
    );
});


builder.Services.AddSingleton<TesiraService>();
builder.Services.AddSingleton<PartyLineConfigService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();


var tesira =
    app.Services.GetRequiredService<TesiraService>();

var config = app.Services.GetRequiredService<PartyLineConfigService>();
await tesira.InitializeAsync();


app.MapGet("/api/matrix", (PartyLineConfigService config) =>
{
    return Results.Ok(new
    {
        outputs = tesira.Outputs,
        inputs = tesira.Inputs,

        forcedCrosspoints = config.ForcedCrosspoints.Select(x => new
        {
            x.Input,
            x.Output,
            State = x.State.ToString()
        })
    });
});


app.MapGet(
    "/api/output/{output:int}",
    async (
        int output) =>
{
    return Results.Ok(
        await tesira.GetOutputStateAsync(output));
});

app.MapPost(
    "/api/crosspoint",
    async (
        CrosspointRequest request) =>
{
    await tesira.SetCrosspointAsync(
        request.Input,
        request.Output,
        request.Connected);

    return Results.Ok();
});
app.MapGet("/api/labels", () =>
{
    return Results.Ok(new
    {
        inputs = tesira.Inputs,
        outputs = tesira.Outputs
    });
});
app.MapPost(
    "/api/inputlabel",
    async (
        LabelRequest request) =>
    {
        await tesira.SetInputLabelAsync(
            request.Number,
            request.Name);

        return Results.Ok();
    });
app.MapPost(
    "/api/outputlabel",
    async (
        LabelRequest request) =>
    {
        await tesira.SetOutputLabelAsync(
            request.Number,
            request.Name);

        return Results.Ok();
    });
app.MapGet("/api/partylines", () =>
{
    return Results.Ok(config.PartyLines);
});
app.MapPost("/api/partyline", (CreatePartyLineRequest req) =>
{
    var pl = new PartyLine
    {
        Id = config.PartyLines.Count == 0
            ? 1
            : config.PartyLines.Max(x => x.Id) + 1,

        Name = req.Name
    };

    config.PartyLines.Add(pl);
    config.Save();

    return Results.Ok(pl);
});
app.MapPost("/api/partyline/add-input", async (PartyLineRequest req) =>
{
    await tesira.AddInput(req.PlId, req.Input);
    config.Save();

    return Results.Ok();
});
app.MapPost("/api/partyline/add-output", async (PartyLineRequest req) =>
{
    await tesira.AddOutput(req.PlId, req.Output);
    config.Save();

    return Results.Ok();
});
app.MapPost("/api/partyline/remove-input",
async (PartyLineRequest req, TesiraService tesira) =>
{
    await tesira.RemoveInput(req.PlId, req.Input);
    return Results.Ok();
});
app.MapPost("/api/partyline/remove-output",
async (PartyLineRequest req, TesiraService tesira) =>
{
    await tesira.RemoveOutput(req.PlId, req.Output);
    return Results.Ok();
});

app.MapPost("/api/partyline/rename", 
    async (RenameRequest req,TesiraService tesira) =>
{
    await tesira.RenamePartyLine(req.PlId, req.Name);
    return Results.Ok();
});
app.MapPost("/api/partyline/delete", 
    async (DeleteRequest req, TesiraService tesira) =>
{
    await tesira.DeletePartyLine(req.PlId);
    return Results.Ok();
});
app.MapPost("/api/crosspoint/force",
async (ForceCrosspointRequest req,
TesiraService tesira) =>
{
    await tesira.ForceCrosspoint(
        req.Input,
        req.Output,
        req.State);

    return Results.Ok();
});
app.MapPost("/api/crosspoint/clear",
async (ClearCrosspointRequest req,
TesiraService tesira) =>
{
    await tesira.ClearForcedCrosspoint(
        req.Input,
        req.Output);

    return Results.Ok();
});

foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
{
    if (ni.OperationalStatus != OperationalStatus.Up)
        continue;

    foreach (var addr in ni.GetIPProperties().UnicastAddresses)
    {
        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
        {
            Console.WriteLine($"Starting Web Interface at: http://{addr.Address}:7000");
        }
    }
}
app.Run("http://0.0.0.0:7000");