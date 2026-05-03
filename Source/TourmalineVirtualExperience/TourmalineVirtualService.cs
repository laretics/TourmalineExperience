using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using Microsoft.AspNetCore.Hosting.Infrastructure;
using Microsoft.Extensions.Configuration;
using TourmalineVirtualExperience;

public class TourmalineVirtualService
{
    private Process? mvarSimulatorProcess;
    private IConfiguration mvarConfig;
    private ILogger<TourmalineVirtualService> mvarLogger;
    private readonly string mvarRuntimePath;
    private readonly string mvarMSTSPath;
    private const string MSTS_ROUTES_PATH = "ROUTES";
    private const string MSTS_PATHS_PATH = "PATHS";
    private const string MSTS_TRAINS_PATH = "TRAINS";
    private const string MSTS_CONSISTS_PATH = "CONSISTS";

    public TourmalineVirtualService(IConfiguration config,
                                    ILogger<TourmalineVirtualService> logger
                                    )
    {
        mvarConfig = config;
        mvarLogger = logger;
        mvarRuntimePath = mvarConfig["OpenRails:RunActivityPath"]
            ?? @"C:\Program Files\Open Rails\RunActivity.exe";
        mvarMSTSPath = mvarConfig["OpenRails:MSTSPath"]
            ?? @"C:\MSTS";

        if (!File.Exists(mvarRuntimePath))
        {
            Console.WriteLine($"[WARNING] No se encontró RunActivity.exe en: {mvarRuntimePath}");
        }
    }

    public async Task<LaunchResult> LaunchOpenRailsAsync(LaunchRequest request)
    {
        if (mvarSimulatorProcess != null && !mvarSimulatorProcess.HasExited)
            return new LaunchResult { Success = false, Message = "Ya hay una simulación en ejecución." };

        string exePath = mvarRuntimePath; //string.IsNullOrWhiteSpace(request.OpenRailsPath)
            //? mvarRuntimePath
            //: request.OpenRailsPath;


        if (!File.Exists(exePath))
            return new LaunchResult { Success = false, Message = $"No se encontró RunActivity.exe en: {exePath}" };

        try
        {
            //-start -exploreactivity A:\ORTS\MSTS\ROUTES\SFM\PATHS\T12.pat A:\ORTS\MSTS\TRAINS\CONSISTS\440.con 12:00 1 0 t

            var arguments = BuildCommandLine(request);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = true,
                CreateNoWindow = false
            };

            mvarSimulatorProcess = Process.Start(startInfo);

            return new LaunchResult
            {
                Success = true,
                Message = "RunActivity iniciado correctamente.",
                ProcessId = mvarSimulatorProcess?.Id,
                CommandLine = $"{exePath} {arguments}"
            };
        }
        catch (Exception ex)
        {
            return new LaunchResult { Success = false, Message = $"Error al iniciar: {ex.Message}" };
        }
    }

    public async Task<StopResult> StopOpenRailsAsync()
    {
        StopResult salida = new StopResult();
        StringBuilder texto = new StringBuilder();
        int contador = 0;
        if (mvarSimulatorProcess == null || mvarSimulatorProcess.HasExited)
        {
            texto.AppendLine("No hay ningón proceso gestionado en ejecución.");
        }
        else
        {
            try
            {
                contador++;
                mvarSimulatorProcess.Kill(true);
                await mvarSimulatorProcess.WaitForExitAsync();
                mvarSimulatorProcess = null;
                texto.AppendLine("Simulación detenida correctamente.");
            }
            catch (Exception ex)
            {
                contador = 0;
                texto.AppendLine($"Error al detener: {ex.Message}");
            }
        }
        
        //Si no ha habido errores, intentamos parar todos los procesos abiertos del simulador.
        foreach (var auxProcess in Process.GetProcessesByName("RunActivity"))
        {
            int id = auxProcess.Id;
            texto.AppendLine($"Parando proceso {id}");
            contador++;
        }
        salida.Success = contador > 0;
        if (salida.Success)
            texto.AppendLine($"Procesos detenidos en total: {contador}");

        salida.Message = texto.ToString();
        return salida;
    }

    public async Task<object> SendCommandAsync(object commandJson)
    {
        if (null== mvarSimulatorProcess  || mvarSimulatorProcess.HasExited)
            return new { Success = false, Message = "There are no running ORTS instances." };

        try
        {
            using (var client = new NamedPipeClientStream(".", "TourmalinePipe", PipeDirection.InOut))
            {
                await client.ConnectAsync(2000);

                var writer = new StreamWriter(client, System.Text.Encoding.UTF8) { AutoFlush = true };
                var reader = new StreamReader(client, System.Text.Encoding.UTF8);

                string json = System.Text.Json.JsonSerializer.Serialize(commandJson);
                await writer.WriteLineAsync(json);
                await writer.FlushAsync();

                string? response = await reader.ReadLineAsync();
                return new { Success = true, Message = "Sending OK", Response = response };
            }
        }
        catch (Exception ex)
        {
            return new { Success = false, Message = $"Error enviando comando: {ex.Message}" };
        }
    }

    public TourmalineProcessStatus? GetStatus()
    {
        return new TourmalineProcessStatus
        {
            IsRunning = mvarSimulatorProcess != null && !mvarSimulatorProcess.HasExited,
            ProcessId = null==mvarSimulatorProcess?-1:mvarSimulatorProcess.Id,
            ExecutablePath = mvarRuntimePath,
            StartTime = null==mvarSimulatorProcess?DateTime.MinValue:mvarSimulatorProcess.StartTime,
        };
    }

    private string BuildCommandLine(LaunchRequest req)
    {        
        var args = new List<string>();
        args.Add("-start");
        args.Add("-exploreactivity");
        string fullPathsPath = Path.Combine(mvarMSTSPath, MSTS_ROUTES_PATH, req.Route, MSTS_PATHS_PATH, req.RoutePath);
        args.Add(string.Format("{0}.pat", fullPathsPath));
        string consistPath = Path.Combine(mvarMSTSPath, MSTS_TRAINS_PATH, MSTS_CONSISTS_PATH, req.Consist);
        args.Add(string.Format("{0}.con", consistPath));
        args.Add(req.Now);
        args.Add(string.Format("{0}", req.Season));
        args.Add(string.Format("{0}", req.Climate));
        args.Add("t"); //Modo tourmaline        
        return string.Join(" ", args);
    }
}

/*
 
  {
  "Type": 3,
  "Data": {
    "objectiveSpeed": 80,
    "InsideLights": true,
    "OutsideLights": 2,
    "Pantograph": true,
    "LeftDoors": false,
    "RightDoors": false,
    "Autopilot": false
  }
}

{
  "Type": 2,
  "Data": {
    "clouds": 0.7,
    "visibility": 5000,
    "precipitation": 0.02,
    "liquidity": 0.5
  }
}

Modo drone
{
  "Type": 1,
  "Data": {
    "Order": 3,
    "Side": false
  }
}


*/
