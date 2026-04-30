namespace TourmalineVirtualExperience
{
    public class TourmalineCommand
    {
        public string Type { get; set; } = string.Empty;
        public object? Data { get; set; }
    }
    public enum TourmalineCameraOrder : byte
    {
        None = 0,
        Cenital = 1,
        Lateral = 2,
        Drone = 3,
        Orbit = 4,
        TrackSide=5,
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

}
