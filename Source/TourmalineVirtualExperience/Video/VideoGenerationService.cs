namespace TourmalineVirtualExperience.Video
{
    public class VideoGenerationService:BackgroundService
    {
        private readonly VideoSegmentGenerator mvarSegmentGenerator;
        public VideoGenerationService(VideoSegmentGenerator segmentGenerator)
        {
            mvarSegmentGenerator = segmentGenerator;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            mvarSegmentGenerator.EnsureStarted();
            Console.WriteLine("[VideoGenerationService] Servicio de generación de vídeo iniciado en background");

            //Mantenemos vivo el servicio.
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
