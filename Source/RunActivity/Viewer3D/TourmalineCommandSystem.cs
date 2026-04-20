using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using EmbedIO.Utilities;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Orts.Simulation.Physics;

namespace Orts.Viewer3D
{
    public class TourmalineCommandSystem
    {
        private readonly Viewer mvarViewer;
        private readonly string mvarPipeName = "TourmalinePipe";
        private NamedPipeServerStream mvarPipeServer;
        private Thread mvarListenerThread;
        private bool mvarRunning;

        public TourmalineCommandSystem(Viewer viewer)
        {
            mvarViewer = viewer;
        }

        public void Start()
        {
            if (mvarRunning) return;

            mvarRunning = true;
            mvarListenerThread = new Thread(ListenForCommands);
            mvarListenerThread.IsBackground = true;
            mvarListenerThread.Start();

            Console.WriteLine("[Tourmaline] Command system started. Listening on pipe: " + mvarPipeName);
        }

        public void Stop()
        {
            mvarRunning = false;
            if (mvarPipeServer != null)
            {
                try { mvarPipeServer.Dispose(); } catch { }
            }
            Console.WriteLine("[Tourmaline] Command system stopped.");
        }

        private void ListenForCommands()
        {
            while (mvarRunning)
            {
                try
                {
                    mvarPipeServer = new NamedPipeServerStream(mvarPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message);
                    mvarPipeServer.WaitForConnection();

                    using (var reader = new StreamReader(mvarPipeServer))
                    {
                        string jsonCommand = reader.ReadLine();

                        if (!string.IsNullOrEmpty(jsonCommand))
                        {
                            ProcessCommand(jsonCommand);
                        }
                    }

                    if (mvarPipeServer.IsConnected)
                        mvarPipeServer.Disconnect();
                }
                catch (Exception ex)
                {
                    if (mvarRunning)
                        Console.WriteLine("[Tourmaline Command Error] " + ex.Message);
                }
            }
        }

        private void ProcessCommand(string jsonCommand)
        {
            try
            {
                TourmalineCommand envelope = JsonConvert.DeserializeObject<TourmalineCommand>(jsonCommand);
                if (null == envelope|| null== envelope.Type || envelope.Type.Length<1) return;
                
                switch(envelope.Type.ToLower())
                {
                    case "camera":
                        if(null!=envelope.Data)
                        {
                            TourmalineCameraCommand cameraCmd = JsonConvert.DeserializeObject<TourmalineCameraCommand>(envelope.Data.ToString());
                            if (null != cameraCmd && null != mvarViewer.TourmalineCamera)
                                ExecuteCameraCommand(cameraCmd);
                        }
                        break;

                    case "weather":
                        if(null!=envelope.Data)
                        {
                            TourmalineWeatherCommand weatherCmd = JsonConvert.DeserializeObject<TourmalineWeatherCommand>(envelope.Data.ToString());
                            if(null!=weatherCmd)
                                ExecuteClimateCommand(weatherCmd);
                        }
                        break;
                    case "train":
                        if(null!=envelope.Data)
                        {
                            TourmalineTrainCommand trainCmd = JsonConvert.DeserializeObject<TourmalineTrainCommand>(envelope.Data.ToString());
                            if(null!=trainCmd) 
                                ExecuteTrainCommand(trainCmd);

                        }
                        break;

                    default:
                        Console.WriteLine($"[Tourmaline] Unknown command type: {envelope.Type}");
                        break; 
                }                                   
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Tourmaline] Error processing command: " + ex.Message);
            }
        }

        private void ExecuteCameraCommand(TourmalineCameraCommand cmd)
        {
            if (cmd.Action == null)
                return;

            string action = cmd.Action.ToLower();

            if (action == "setcenitalview")
            {
                mvarViewer.TourmalineCamera.SetCenitalView();
            }
            else if (action == "settraseraelevadaview")
            {
                mvarViewer.TourmalineCamera.SetTraseraElevadaView();
            }
            else if (action == "setlateralview")
            {
                bool isLeft = (cmd.Side != null && cmd.Side.ToLower() == "left");
                mvarViewer.TourmalineCamera.SetLateralView(isLeft);
            }
            else if(action=="orbit")
            {
                if(cmd.OrbitSpeed.HasValue)
                {
                    mvarViewer.TourmalineCamera.SetOrbitMode(cmd.OrbitSpeed.Value);
                }                
            }
            else
            {
                Console.WriteLine("[Tourmaline] Unknown camera action: " + cmd.Action);
            }
        }
        private void ExecuteClimateCommand(TourmalineWeatherCommand cmd)
        {
            WeatherControl auxControl = mvarViewer.World.WeatherControl;
            if (cmd.clouds.HasValue)
                auxControl.Weather.CloudCoverFactor = MathHelper.Clamp(cmd.clouds.Value, 0f, 1f);
            if (cmd.visibility.HasValue)
                auxControl.Weather.VisibilityM = MathHelper.Clamp(cmd.visibility.Value, 10f, 100000f);
            if (cmd.precipitation.HasValue)
                auxControl.Weather.PrecipitationIntensityPPSPM2 = MathHelper.Clamp(cmd.precipitation.Value, 0f, 0.05f);
            if (cmd.liquidity.HasValue)
                auxControl.Weather.PrecipitationLiquidity = MathHelper.Clamp(cmd.liquidity.Value, 0f, 1f);
            Console.WriteLine($"[Tourmaline] Weather updated - Cloud: { auxControl.Weather.CloudCoverFactor:F2}, " +
                      $"Visibility: {auxControl.Weather.VisibilityM:F0}m, " +
                      $"Precip: {auxControl.Weather.PrecipitationIntensityPPSPM2:F5}");
        }
        private void ExecuteTrainCommand(TourmalineTrainCommand cmd)
        {
            if(cmd.objectiveSpeed.HasValue)
            {
                Train playerTrain = mvarViewer.PlayerLocomotive?.Train;
                if(null!=playerTrain)
                {
                    float targetSpeedMpS = cmd.objectiveSpeed.Value / 3.6f;
                    playerTrain.ForcedSpeedMpS = targetSpeedMpS;
                    Console.WriteLine($"[Tourmaline] Player train target speed set to {cmd.objectiveSpeed.Value:F1} Km/h");
                }
                else
                {
                    Console.WriteLine("[Tourmaline] No PlayerTrain found");
                }
            }
        }
    }


    public class TourmalineCommand
    {
        public string Type { get; set; } = string.Empty;
        public object Data { get; set; } //Contenido del comando
    }

    // Clase auxiliar para los comandos
    public class TourmalineCameraCommand
    {        
        public string Action { get; set; }
        public string Side { get; set; }
        public float? Distance { get; set; }
        public float? Azimuth { get; set; }
        public float? Elevation { get; set; }
        public float? OrbitSpeed{ get; set; }

        public TourmalineCameraCommand()
        {
            Action = "";
            Side = "";
        }
    }
    public class TourmalineWeatherCommand
    {
        public float? clouds { get; set; }
        public float? visibility { get; set; }
        public float? precipitation { get; set; }
        public float? liquidity { get; set; }
    }
    public class TourmalineTrainCommand
    {
        public int? objectiveSpeed{ get; set; } //Velocidad objetivo
    }
}
