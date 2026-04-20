using System.Diagnostics;

namespace TourmalineVirtualExperience
{
    public class MediaMTXManager:IHostedService,IDisposable
    {
        private Process? mvarProcess;
        private readonly IConfiguration mvarConfig;
        private readonly ILogger<MediaMTXManager> mvarLogger;

        public bool IsRunning => null != mvarProcess && !mvarProcess.HasExited;

        public MediaMTXManager(IConfiguration config, ILogger<MediaMTXManager> logger)
        {
            mvarConfig = config;
            mvarLogger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if(!mvarConfig.GetValue<bool>("MediaMTX:Enabled",true))
            {
                mvarLogger.LogInformation("MediaMTX is disabled by config value");
                return;
            }

            string? auxExePath = mvarConfig["MediaMTX:ExecPath"];
            string? auxConfigPath = mvarConfig["MediaMTX:ConfigPath"];
            if(null!=auxExePath)
            {
                string exePath = Path.Combine(auxExePath, "MediaMTX.exe");                
                if(!File.Exists(exePath))
                {
                    throw new FileNotFoundException($"Couldn't find MediaMTX in {auxExePath}");
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"\"MediaMTX.yml\"",
                    WorkingDirectory = auxExePath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                mvarProcess = new Process{ StartInfo=startInfo };
                mvarProcess.OutputDataReceived += (sender, e) => mvarLogger.LogInformation($"MediaMTX: {e.Data}");
                mvarProcess.ErrorDataReceived += (sender, e) => mvarLogger.LogError($"MediaMTX Error: {e.Data}");

                try
                {
                    mvarProcess.Start();
                    mvarProcess.BeginOutputReadLine();
                    mvarProcess.BeginErrorReadLine();

                    mvarLogger.LogInformation("MediaMTX iniciado correctamente.");
                    await Task.Delay(1200, cancellationToken); //Espera para estabilización
                }
                catch (Exception ex)
                {
                    mvarLogger.LogError(ex, "MediaMTX initialisation error");
                }            
            }                       
        }
        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stop();
            return Task.CompletedTask;
        }

        public void Stop()
        {
            if (null == mvarProcess || mvarProcess.HasExited)
                return;

            try
            {
                mvarLogger.LogInformation("Stopping MediaMTX...");
                mvarProcess.Kill();
                if (!mvarProcess.WaitForExit(5000))
                    mvarProcess.Kill(); //Forzamos cierre.
                
                mvarLogger.LogInformation("MediaMTX stopped");
            }
            catch (Exception ex) 
            {
                mvarLogger.LogError(ex, "Error stopping MediaMTX");
            }
            finally
            {
                mvarProcess.Dispose();
                mvarProcess = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }


    }
}
