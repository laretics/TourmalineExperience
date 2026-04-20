using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Extensions.Configuration;
using TourmalineVirtualExperience;

public class TourmalineVirtualService
{
    private Process? mvarCurrentProcess;
    private readonly string mvarRuntimePath;
    private readonly string mvarMSTSPath;
    private const string MSTS_ROUTES_PATH = "ROUTES";
    private const string MSTS_PATHS_PATH = "PATHS";
    private const string MSTS_TRAINS_PATH = "TRAINS";
    private const string MSTS_CONSISTS_PATH = "CONSISTS";

    public TourmalineVirtualService(IConfiguration config)
    {
        mvarRuntimePath = config["OpenRails:RunActivityPath"]
            ?? @"C:\Program Files\Open Rails\RunActivity.exe";
        mvarMSTSPath = config["OpenRails:MSTSPath"]
            ?? @"C:\MSTS";

        if (!File.Exists(mvarRuntimePath))
        {
            Console.WriteLine($"[WARNING] No se encontró RunActivity.exe en: {mvarRuntimePath}");
        }
    }

    public async Task<LaunchResult> LaunchOpenRailsAsync(LaunchRequest request)
    {
        if (mvarCurrentProcess != null && !mvarCurrentProcess.HasExited)
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

            mvarCurrentProcess = Process.Start(startInfo);

            return new LaunchResult
            {
                Success = true,
                Message = "RunActivity iniciado correctamente.",
                ProcessId = mvarCurrentProcess?.Id,
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
        if (mvarCurrentProcess == null || mvarCurrentProcess.HasExited)
            return new StopResult { Success = false, Message = "No hay ninguna simulación en ejecución." };

        try
        {
            mvarCurrentProcess.Kill(true);
            await mvarCurrentProcess.WaitForExitAsync();
            mvarCurrentProcess = null;

            return new StopResult { Success = true, Message = "Simulación detenida correctamente." };
        }
        catch (Exception ex)
        {
            return new StopResult { Success = false, Message = $"Error al detener: {ex.Message}" };
        }
    }

    public async Task<object> SendCommandAsync(object commandJson)
    {
        if (null== mvarCurrentProcess  || mvarCurrentProcess.HasExited)
            return new { Success = false, Message = "No hay ninguna simulación en ejecución." };

        try
        {
            using (var client = new NamedPipeClientStream(".", "TourmalinePipe", PipeDirection.Out))
            {
                await client.ConnectAsync(2000);   // timeout 2 segundos

                using (var writer = new StreamWriter(client))
                {
                    string json = System.Text.Json.JsonSerializer.Serialize(commandJson);
                    await writer.WriteLineAsync(json);
                    await writer.FlushAsync();
                }
            }

            return new { Success = true, Message = "Comando enviado correctamente al simulador." };
        }
        catch (Exception ex)
        {
            return new { Success = false, Message = $"Error enviando comando: {ex.Message}" };
        }
    }

    public object GetStatus()
    {
        return new
        {
            IsRunning = mvarCurrentProcess != null && !mvarCurrentProcess.HasExited,
            ProcessId = mvarCurrentProcess?.Id,
            ExecutablePath = mvarRuntimePath
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
