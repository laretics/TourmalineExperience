using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using EmbedIO.Utilities;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Orts.Common;
using Orts.Formats.Msts;
using Orts.Simulation;
using Orts.Simulation.AIs;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.Viewer3D.Tourmaline
{
    public class TourmalineCommandSystem
    {
        private readonly Viewer mvarViewer;
        private readonly string mvarPipeName = "TourmalinePipe";
        private Thread mvarListenerThread;
        private bool mvarRunning;

        public TourmalineCommandSystem(Viewer viewer)
        {
            mvarPipeName = ConfigurationManager.AppSettings["CommandPipeName"];
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
            Console.WriteLine("[Tourmaline] Command system stopped.");
        }

        private void ListenForCommands()
        {
            while (mvarRunning)
            {
                try
                {
                    using (NamedPipeServerStream auxPipeServer = new NamedPipeServerStream(mvarPipeName,PipeDirection.InOut,1,PipeTransmissionMode.Message))
                    {
                        auxPipeServer.WaitForConnection();
                        StreamReader auxReader = new StreamReader(auxPipeServer, System.Text.Encoding.UTF8);
                        string jsonCommand = auxReader.ReadLine();
                        StreamWriter auxWriter = new StreamWriter(auxPipeServer, System.Text.Encoding.UTF8) { AutoFlush=true};
                        bool responded = false;

                        try
                        {
                            if (!string.IsNullOrEmpty(jsonCommand))
                            {
                                ProcessCommand(jsonCommand, auxWriter);
                                responded = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[Tourmaline] Error interno: " + ex.Message);
                            // Siempre responde aunque haya error
                            auxWriter.WriteLine("{\"success\":false,\"error\":\"" + ex.Message.Replace("\"", "\\\"") + "\"}");
                            responded = true;
                        }
                        // Si por alguna razón no se respondió, responde aquí
                        if (!responded)
                            auxWriter.WriteLine("{\"success\":false,\"error\":\"No command processed\"}");

                    }
                }
                catch (Exception ex)
                {
                    if (mvarRunning)
                        Console.WriteLine("[Tourmaline Command Error] " + ex.Message);
                }
            }
        }

        private void ProcessCommand(string jsonCommand, StreamWriter writer)
        {
            try
            {
                TourmalineCommand envelope = JsonConvert.DeserializeObject<TourmalineCommand>(jsonCommand);
                if (null == envelope) return;
                
                switch(envelope.Type)
                {
                    case TourmalineCommandType.Camera:
                        if(null!=envelope.Data)
                        {
                            TourmalineCameraCommand cameraCmd = JsonConvert.DeserializeObject<TourmalineCameraCommand>(envelope.Data.ToString());
                            if (null != cameraCmd && null != mvarViewer.TourmalineCamera)
                            {                                
                                ExecuteCameraCommand(cameraCmd);
                                SendTelemetryResponse(writer, true);
                            }
                        }
                        break;

                    case TourmalineCommandType.Weather:
                        if(null!=envelope.Data)
                        {
                            TourmalineWeatherCommand weatherCmd = JsonConvert.DeserializeObject<TourmalineWeatherCommand>(envelope.Data.ToString());
                            if(null!=weatherCmd)
                            {
                                ExecuteClimateCommand(weatherCmd);
                                SendTelemetryResponse(writer, true);
                            }
                        }
                        break;
                    case TourmalineCommandType.Simulation:
                        if(null!=envelope.Data)
                        {
                            TourmalineTrainCommand trainCmd = JsonConvert.DeserializeObject<TourmalineTrainCommand>(envelope.Data.ToString());
                            if(null!=trainCmd) 
                            {
                                ExecuteTrainCommand(trainCmd);
                                SendTelemetryResponse(writer, true);
                            }                                
                        }
                        break;
                    default:
                        Console.WriteLine($"[Tourmaline] Unknown command type: {envelope.Type}");
                        SendTelemetryResponse(writer, false);
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
            switch (cmd.Order)
            {
                case TourmalineCameraOrder.Cenital:
                    auxSwitchTourmalineCam();
                    mvarViewer.TourmalineCamera.SetCenitalView();
                    break;
                case TourmalineCameraOrder.Lateral:
                    auxSwitchTourmalineCam();
                    mvarViewer.TourmalineCamera.SetLateralView(cmd.Side);
                    break;
                case TourmalineCameraOrder.Drone:
                    auxSwitchTourmalineCam();
                    mvarViewer.TourmalineCamera.SetDroneView(cmd.Side);
                    break;
                case TourmalineCameraOrder.Orbit:
                    auxSwitchTourmalineCam();
                    mvarViewer.TourmalineCamera.SetOrbitMode(cmd.Speed ? 10f : 4f);
                    break;
                case TourmalineCameraOrder.TrackSide:
                    if (mvarViewer.Camera != mvarViewer.TracksideCamera)
                    {
                        mvarViewer.Camera = mvarViewer.TracksideCamera;
                        mvarViewer.Camera.Activate();
                    }
                    break;
                case TourmalineCameraOrder.Brakeman:
                    if(mvarViewer.Camera !=mvarViewer.BrakemanCamera)
                    {
                        mvarViewer.Camera = mvarViewer.BrakemanCamera;
                        mvarViewer.Camera.Activate();
                    }
                    break;
                default:
                    Console.WriteLine("[Tourmaline] Unknown camera action: " + cmd.Order);
                    break;                
            }
        }
        private void auxSwitchTourmalineCam()
        {
            if (mvarViewer.Camera != mvarViewer.TourmalineCamera)
            {
                mvarViewer.Camera = mvarViewer.TourmalineCamera;
                mvarViewer.Camera.Activate();
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
            Train playerTrain = mvarViewer.PlayerLocomotive?.Train;
            if(null==playerTrain)
            {
                Console.WriteLine("[Tourmaline] No PlayerTrain found");
            }
            else
            {
                if (cmd.objectiveSpeed.HasValue)
                {
                    float targetSpeedMpS = cmd.objectiveSpeed.Value / 3.6f;
                    playerTrain.ForcedSpeedMpS = targetSpeedMpS;
                    Console.WriteLine($"[Tourmaline] Player train target speed set to {cmd.objectiveSpeed.Value:F1} Km/h");
                }
                if (cmd.Autopilot.HasValue)
                {
                    if (cmd.Autopilot.Value)
                    {
                        if(!playerTrain.Autopilot)
                        {
                            bool success = ((AITrain)playerTrain).SwitchToAutopilotControl();
                            if(success)
                                Console.WriteLine($"[Tourmaline] Player train now running on autopilot");
                        }
                    }
                    else
                    {
                        if(playerTrain.Autopilot)
                        {
                            playerTrain.RequestToggleManualMode();
                            Console.WriteLine($"[Tourmaline] Player train now running on manual mode");
                        }                            
                    }                        
                }
                if(cmd.InsideLights.HasValue || cmd.Pantograph.HasValue ||cmd.OutsideLights.HasValue)
                {
                    TrainCar auxLoco = mvarViewer.PlayerLocomotive;                    
                    if (null != auxLoco)
                    {
                        MSTSLocomotive locomotive = (MSTSLocomotive)auxLoco;
                        if (cmd.InsideLights.HasValue)
                        {
                            locomotive.CabLightOn = cmd.InsideLights.Value;
                            Console.WriteLine("[Tourmaline] Inside lights changed");
                        }                            
                        if (cmd.OutsideLights.HasValue)
                        {
                            switch (cmd.OutsideLights.Value)
                            {
                                case 0: locomotive.SignalEvent(Orts.Common.Event._HeadlightOff); break;
                                case 1: locomotive.SignalEvent(Orts.Common.Event._HeadlightDim); break;
                                case 2: locomotive.SignalEvent(Orts.Common.Event._HeadlightOn); break;
                            }
                            Console.WriteLine("[Tourmaline] Outside lights changed");
                        }
                        if(cmd.Pantograph.HasValue)
                        {
                            if(locomotive is MSTSElectricLocomotive)
                            {
                                locomotive.SignalEvent(cmd.Pantograph.Value? Orts.Common.Event.Pantograph1Up: Orts.Common.Event.Pantograph1Down);
                                locomotive.SignalEvent(cmd.Pantograph.Value ? Orts.Common.Event.Pantograph2Up : Orts.Common.Event.Pantograph2Down);
                                Console.WriteLine("[Tourmaline] Pantographs changed");
                            }
                        }
                        if(cmd.LeftDoors.HasValue)
                        {
                            playerTrain.SetDoors(Simulation.RollingStocks.SubSystems.DoorSide.Left, cmd.LeftDoors.Value);
                            Console.WriteLine("[Tourmaline] Left doors changed");
                        }                            
                        if (cmd.RightDoors.HasValue)
                        {
                            playerTrain.SetDoors(Simulation.RollingStocks.SubSystems.DoorSide.Right, cmd.RightDoors.Value);
                            Console.WriteLine("[Tourmaline] Right doors changed");
                        }                        
                    }
                }
            }
        }
        private void SendTelemetryResponse(StreamWriter writer, bool success)
        {
            TourmalineTelemetryResponse response = new TourmalineTelemetryResponse();
            response.success = success;
            Train playerTrain = mvarViewer.PlayerLocomotive?.Train;
            if (null != playerTrain)
            {
                response.Speed = (int)(playerTrain.SpeedMpS * 3.6f);
                double latitude = 0;
                double longitude = 0;
                Traveller auxTraveller = playerTrain.FrontTDBTraveller;
                new WorldLatLon().ConvertWTC
                (auxTraveller.TileX,
                auxTraveller.TileZ,
                auxTraveller.Location,
                ref latitude, ref longitude);
                response.Latitude = MathHelper.ToDegrees((float)latitude);
                response.Longitude = MathHelper.ToDegrees((float)longitude);
            }           
            string responseJson = JsonConvert.SerializeObject(response);
            
            try
            {
                if(null!=writer)
                {                    
                    writer.WriteLine(responseJson);
                    writer.Flush();
                }
            }
            catch(Exception ex) 
            {
                Console.WriteLine("[Tourmaline] Error sending telemetry: " + ex.Message);
            }
        }
    }

    public class TourmalineCommand
    {
        public TourmalineCommandType Type { get; set; } = TourmalineCommandType.None;
        public object Data { get; set; } //Contenido del comando
    }
    public enum TourmalineCommandType : byte
    {
        None = 0,
        Camera = 1,
        Weather = 2,
        Simulation = 3,
        Other = 255
    }
    public enum TourmalineCameraOrder:byte
    {
        None = 0,
        Cenital = 1,
        Lateral = 2,
        Drone=3,
        Orbit = 4,
        TrackSide = 5,
        Brakeman = 6,
        Other = 255
    }
    // Clase auxiliar para los comandos
    public class TourmalineCameraCommand
    {
        public TourmalineCameraOrder Order { get; set; }
        public bool Side { get; set; }
        public bool Speed { get; set; }
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
        public bool? InsideLights { get; set; } //Apagado o encendido
        public byte? OutsideLights { get; set; } //0: Apagado , 1: Cortas, 2: Largas
        public bool? Pantograph{ get; set; } //Pantógrafos
        public bool? LeftDoors { get; set; }
        public bool? RightDoors{ get; set; }
        public bool? Autopilot{ get; set; } //Piloto automático del modo demo
    }
    public class TourmalineTelemetryResponse
    {
        public bool success{ get; set; }
        public double Latitude{ get; set; }
        public double Longitude{ get; set; }
        public int Speed{ get; set; }
    }
}
