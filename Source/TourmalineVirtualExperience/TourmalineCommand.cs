namespace TourmalineVirtualExperience
{
    public class TourmalineCommand
    {
        public TourmalineCommandType Type { get; set; } = TourmalineCommandType.None;
        public object? Data { get; set; } //Contenido del comando
    }
    public enum TourmalineCommandType : byte
    {
        None = 0,
        Camera=1,
        Weather=2,
        Simulation=3,       
        Other = 255
    }
    public enum TourmalineCameraOrder : byte
    {
        None = 0,
        Cenital = 1,
        Lateral = 2,
        Drone = 3,
        Orbit = 4,
        TrackSide=5,
        Brakeman=6,
        Other = 255
    }

    public class TourmalineCameraCommand
    {
        public TourmalineCameraOrder Order { get; set;  }
        public bool Side{ get; set; }
        public bool Speed { get; set; }
    }

    public class TourmalineWeatherCommand
    {
        public float? clouds { get; set; }
        public float? visibility { get; set; }
        public float? precipitation{ get; set; }
        public float? liquidity{ get; set; }        
    }
    public class TourmalineTrainCommand
    {
        public int? objectiveSpeed { get; set; } //Velocidad objetivo
        public bool? InsideLights { get; set; } //Apagado o encendido
        public byte? OutsideLights { get; set; } //0: Apagado , 1: Cortas, 2: Largas
        public bool? Pantograph { get; set; } //Pantógrafos
        public bool? LeftDoors { get; set; }
        public bool? RightDoors { get; set; }
        public bool? Autopilot { get; set; } //Piloto automático del modo demo
    }
    public class TourmalineTelemetryResponse
    {
        public bool success { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Speed { get; set; }
    }
}
