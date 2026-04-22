using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System;


namespace TourmalineVirtualExperience.Video
{
/// <summary>
/// Generador de segmentos de vídeo reproducibles en navegadores.
/// Todo esto queda en la memoria RAM.
/// </summary>
    public class VideoSegmentGenerator:IDisposable
    {
        private readonly ConcurrentDictionary<int, byte[]> mcolSegmentsInMemory = new();
        private int mvarNextSegmentId = 1;
        private bool mvarIsRunning = false;
        private Task? mvarGenerationTask;

        //Valores de configuración
        private const int TARGET_WIDTH = 800;
        private const int TARGET_HEIGHT = 600;
        private const int TARGET_FPS = 20;

        public VideoSegmentGenerator()
        {
        }

       private float GetCurrentAcceleration()
        {
            // Aquí leeremos SpeedMpS del tren y calcularemos aceleración
            return 0.0f;
        }
        private float ComputeSegmentDuration(float acceleration)
        {
            float absAccel = Math.Abs(acceleration);
            float duration = 5.0f - (absAccel * 2.5f);
            return Math.Clamp(duration, 2.0f, 5.0f);
        }

        private async Task<byte[]> GenerateTsSegmentAsync(float durationSeconds)
        {
            string arguments = $"-f rawvideo -pix_fmt bgra -s {TARGET_WIDTH}x{TARGET_HEIGHT} -r {TARGET_FPS} -i - " +
                               $"-c:v libx264 -preset ultrafast -tune zerolatency " +
                               $"-b:v 800k -maxrate 1200k -bufsize 2400k " +
                               $"-pix_fmt yuv420p -g 60 -bf 0 " +
                               $"-f mpegts -t {durationSeconds} pipe:1";

            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg.exe",
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                throw new Exception("No se pudo iniciar FFmpeg");

            // TODO: Aquí enviaremos los frames reales del simulador
            // Por ahora simulamos el tiempo
            await Task.Delay((int)(durationSeconds * 1000));

            // Cerramos la entrada para que FFmpeg termine de codificar
            process.StandardInput.Close();

                    // Leemos todo el output (MPEG-TS) como bytes
                    byte[] segmentData = await ReadStreamToEndAsync(process.StandardOutput.BaseStream);

                    // Esperamos a que termine
                    await process.WaitForExitAsync();

            Console.WriteLine($"[VideoSegment] Segmento generado correctamente: {segmentData.Length / 1024} KB");

            return segmentData;
        }
        private static async Task<byte[]> ReadStreamToEndAsync(Stream stream)
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
        private void CleanOldSegments(int keepLast)
        {
            IEnumerable<int> keysToRemove = mcolSegmentsInMemory.Keys
            .OrderByDescending(k => k)
            .Skip(keepLast)
            .ToList();
            foreach (int key in keysToRemove)
                mcolSegmentsInMemory.TryRemove(key, out _);
        }

        private async Task GenerationLoop()
        {
            while(mvarIsRunning)
            {
                try
                {
                    float currentAcceleration = GetCurrentAcceleration();
                    float segmentDuration = ComputeSegmentDuration(currentAcceleration);

                    byte[] segmentData = await GenerateTsSegmentAsync(segmentDuration);

                    int segmentId = mvarNextSegmentId++;
                    mcolSegmentsInMemory[segmentId] = segmentData;

                    // Notificamos (más adelante conectaremos con SignalR)
                    Console.WriteLine($"[VideoSegment] Segmento {segmentId} generado ({segmentDuration:F1}s) - {segmentData.Length / 1024} KB en memoria");

                    //Limpiamos los segmentos antiguos manteniendo sólo 8
                    CleanOldSegments(8);

                    await Task.Delay(TimeSpan.FromSeconds(segmentDuration - 0.5));
                    //Esperamos lo que tarda el segmento actual menos un pequeño tiempo de overlap.
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[VideoSegment] Error generando segmento: {ex.Message}");
                    await Task.Delay(1000);
                }
            }
        }
        
        public void Start()
        {
            if (mvarIsRunning) return;
            mvarIsRunning = true;
            mvarGenerationTask = Task.Run(GenerationLoop);
            Console.WriteLine("[VideoSegmentGenerator] Iniciado - Generando segmentos MPEG-TS en memoria");
        }
        public void Stop()
        {
            mvarIsRunning = false;
            mvarGenerationTask?.Wait(2000);
            Console.WriteLine("[VideoSegmentGenerator] Detenido");
        }
        public byte[]? GetSegment(int segmentId)
        {
            mcolSegmentsInMemory.TryGetValue(segmentId, out byte[]? salida);
            return salida;            
        }

        public void Dispose()
        {
            Stop();
            mcolSegmentsInMemory.Clear();
        }
    }
}
