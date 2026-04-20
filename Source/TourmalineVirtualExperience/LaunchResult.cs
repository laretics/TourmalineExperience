namespace TourmalineVirtualExperience
{
    public class LaunchResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? ProcessId { get; set; }
        public string CommandLine { get; set; } = string.Empty;
    }
}
