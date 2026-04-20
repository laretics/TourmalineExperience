using Microsoft.AspNetCore.Mvc;
using TourmalineVirtualExperience;

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

// Endpoints
app.MapPost("/launch", async ([FromBody] LaunchRequest request, TourmalineVirtualService service) =>
{
    var result = await service.LaunchOpenRailsAsync(request);
    return Results.Ok(result);
})
.WithName("LaunchOpenRails");

app.MapPost("/stop", async (TourmalineVirtualService service) =>
{
    var result = await service.StopOpenRailsAsync();
    return Results.Ok(result);
})
.WithName("StopOpenRails");

app.MapGet("/status", (TourmalineVirtualService service) =>
{
    return Results.Ok(service.GetStatus());
})
.WithName("GetStatus");

// Comando en tiempo real
app.MapPost("/command", async ([FromBody] TourmalineCommand command, TourmalineVirtualService service) =>
{
    var result = await service.SendCommandAsync(command);
    return Results.Ok(result);
})
.WithName("SendCommand");

app.Run();
