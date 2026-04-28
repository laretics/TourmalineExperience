using System.Reflection.Metadata.Ecma335;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TourmalineVirtualExperience;
using TourmalineVirtualExperience.Video;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

builder.Services.AddSingleton<TourmalineVirtualService>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Tourmaline Experience Launcher");
        options.RoutePrefix = "swagger";
    });

    app.MapGet("/", () => Results.Redirect("/swagger"));
}

app.UseWebSockets();
app.UseMiddleware<WebSocketStreamMiddleware>();
app.UseStaticFiles();

// Endpoints
// ==================== ENDPOINTS ====================

// Launch OpenRails
app.MapPost("/launch", async (
    [FromBody] LaunchRequest request,
    [FromServices] TourmalineVirtualService service) =>
{
    var result = await service.LaunchOpenRailsAsync(request);
    return Results.Ok(result);
})
.WithName("LaunchOpenRails");

// Stop OpenRails
app.MapPost("/stop", async ([FromServices] TourmalineVirtualService service) =>
{
    var result = await service.StopOpenRailsAsync();
    return Results.Ok(result);
})
.WithName("StopOpenRails");

// Status general
app.MapGet("/status", ([FromServices] TourmalineVirtualService service) =>
{
    return Results.Ok(service.GetStatus());
})
.WithName("GetStatus");

// Comando en tiempo real
app.MapPost("/command", async (
    [FromBody] TourmalineCommand command,
    [FromServices] TourmalineVirtualService service) =>
{
    var result = await service.SendCommandAsync(command);
    return Results.Ok(result);
})
.WithName("SendCommand");

app.UseStaticFiles();

app.Run();
