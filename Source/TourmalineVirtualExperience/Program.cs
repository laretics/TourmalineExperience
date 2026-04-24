using System.Reflection.Metadata.Ecma335;
using Microsoft.AspNetCore.Mvc;
using TourmalineVirtualExperience;
using TourmalineVirtualExperience.Video;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

//Servicios principales.
builder.Services.AddSingleton<TourmalineVirtualService>();
builder.Services.AddSingleton<VideoSegmentGenerator>();
builder.Services.AddSingleton<SimulatorFrameReceiver>();

//Servicio de background que inicia la generación.
//builder.Services.AddHostedService<VideoGenerationService>();

var app = builder.Build();

SimulatorFrameReceiver auxSimulatorFrameReceiver = app.Services.GetRequiredService<SimulatorFrameReceiver>();
auxSimulatorFrameReceiver.Start();
//Conectamos este receptor con el generador de segmentos...
auxSimulatorFrameReceiver.FrameReceived += (frameData) =>
{
    //Pasando el frame al generador de segmentos.
    VideoSegmentGenerator auxGenerator = app.Services.GetRequiredService<VideoSegmentGenerator>();
    auxGenerator.EnqueueFrame(frameData);
};

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
app.MapPost("/launch", async ([FromBody] LaunchRequest request, 
        TourmalineVirtualService service, 
        VideoSegmentGenerator auxGenerator) =>
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

app.MapGet("/video/segment/{id:int}", (int id, VideoSegmentGenerator auxGenerator) =>
{    
    Console.WriteLine($"[Endpoint] Solicitado segmento {id}. Total en memoria: {auxGenerator.GetSegmentCount()}");
    var data = auxGenerator.GetSegment(id);

    if (data == null || data.Length == 0)
    {
        return Results.NotFound($"Segmento {id} no encontrado. Total segmentos en memoria: {auxGenerator.GetSegmentCount()}");
    }

    Console.WriteLine($"[Endpoint] Sirviendo segmento {id} ({data.Length / 1024} KB)");
    return Results.File(data, contentType: "video/mp2t", fileDownloadName: $"segment_{id}.ts");
});

app.MapGet("/video/status", (VideoSegmentGenerator auxGenerator) =>
{
    return Results.Json(new
    {
        TotalSegments = auxGenerator.GetSegmentCount(),
        NextSegmentId = auxGenerator.GetNextSegmentId()
    });
});

app.MapGet("/test/encode", () =>
{
    var encoder = new VideoEncoder(800, 600, 20);

    // Crear un frame dummy rojo
    byte[] dummyFrame = new byte[800 * 600 * 4];
    for (int i = 0; i < dummyFrame.Length; i += 4)
    {
        dummyFrame[i + 2] = 255; // R
        dummyFrame[i + 3] = 255; // A
    }

    byte[] h264Data = encoder.EncodeFrame(dummyFrame);
    encoder.Dispose();

    Console.WriteLine($"[Test] Frame codificado: {h264Data.Length} bytes");

    return Results.File(h264Data, "video/h264", "test.h264");
});

app.Run();
