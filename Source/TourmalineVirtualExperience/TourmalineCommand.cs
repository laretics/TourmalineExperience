namespace TourmalineVirtualExperience
{
    public class TourmalineCommand
    {
        public string Type { get; set; } = string.Empty;
        public object? Data { get; set; }
    }


    public class TourmalineCameraCommand
    {
        public string Action{ get; set;  }  = string.Empty;
        public string? Side{ get; set; }
        public float? Distance{ get; set; }
        public float? Azimuth{ get; set; }
        public float? Elevation{ get; set; }
        public float? OrbitSpeed{ get; set; }
    }

    public class TourmalineWeatherCommand
    {
        public float? clouds { get; set; }
        public float? visibility { get; set; }
        public float? precipitation{ get; set; }
        public float? liquidity{ get; set; }        
    }

}
