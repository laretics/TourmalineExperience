using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Xml.Schema;


namespace TourmalineVirtualExperience.Video
{
    /// <summary>
    /// Generador de segmentos de vídeo reproducibles en navegadores.
    /// Todo esto queda en la memoria RAM.
    /// </summary>
    public class VideoSegmentGenerator : IDisposable
    {
        private readonly ConcurrentDictionary<int, byte[]> mcolSegmentsInMemory = new();
        private int mvarNextSegmentId = 1;
        private bool mvarIsRunning = false;
        private byte[]? mvarCurrentFrameBuffer;

        private const int TARGET_WIDTH = 800;
        private const int TARGET_HEIGHT = 600;
        private const int TARGET_FPS = 20;

        private readonly string mvarffMpegPath;

        public VideoSegmentGenerator()
        {
            mvarffMpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "ffmpeg.exe");
            if (!File.Exists(mvarffMpegPath))
                throw new FileNotFoundException($"No se encontró ffmpeg.exe en: {mvarffMpegPath}");

            Console.WriteLine($"[VideoSegmentGenerator] FFmpeg encontrado en: {mvarffMpegPath}");
        }

        public void EnqueueFrame(byte[] frameData, int width, int height)
        {
            if (frameData == null || frameData.Length == 0) return;
            mvarCurrentFrameBuffer = (byte[])frameData.Clone();
            Console.WriteLine($"[VideoSegmentGenerator] Frame real encolado correctamente ({width}x{height})");
        }

        public void EnsureStarted()
        {
            if (mvarIsRunning) return;
            mvarIsRunning = true;
            Task.Run(async () => await GenerationLoop());
            Console.WriteLine("[VideoSegmentGenerator] Generación iniciada bajo demanda");
        }

        private async Task GenerationLoop()
        {
            while (mvarIsRunning)
            {
                try
                {
                    float segmentDuration = 5.0f; // Temporal
                    byte[] segmentData = await GenerateTsSegmentAsync(segmentDuration);

                    int segmentId = mvarNextSegmentId++;
                    mcolSegmentsInMemory[segmentId] = segmentData;

                    Console.WriteLine($"[VideoSegment] Segmento {segmentId} generado - {segmentData.Length / 1024} KB");

                    CleanOldSegments(8);
                    await Task.Delay((int)(segmentDuration * 1000) - 500);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[VideoSegment] Error: {ex.Message}");
                    await Task.Delay(1000);
                }
            }
        }

        private async Task<byte[]> GenerateTsSegmentAsync(float durationSeconds)
        {
            Console.WriteLine($"[VideoSegment] === GENERANDO SEGMENTO de {durationSeconds:F1} segundos ===");

            string tempRawPath = Path.Combine(Path.GetTempPath(), $"tourmaline_{DateTime.Now.Ticks}.raw");

            try
            {
                byte[] frameToSend = mvarCurrentFrameBuffer ?? CreateDummyFrame();
                await File.WriteAllBytesAsync(tempRawPath, frameToSend);

                string arguments = $"-f rawvideo -pix_fmt bgra -s {TARGET_WIDTH}x{TARGET_HEIGHT} -r {TARGET_FPS} -i \"{tempRawPath}\" " +
                                   $"-t {durationSeconds} " +
                                   $"-c:v libx264 -preset medium -crf 23 " +   // mejor calidad
                                   $"-b:v 2000k -maxrate 3000k -bufsize 6000k " +
                                   $"-pix_fmt yuv420p -g 30 " +
                                   $"-f mpegts pipe:1";

                var startInfo = new ProcessStartInfo
                {
                    FileName = mvarffMpegPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    throw new Exception("No se pudo iniciar FFmpeg");

                byte[] segmentData = await ReadStreamToEndAsync(process.StandardOutput.BaseStream);

                await process.WaitForExitAsync(new CancellationTokenSource(15000).Token);

                Console.WriteLine($"[VideoSegment] === SEGMENTO GENERADO === Tamaño: {segmentData.Length / 1024} KB");

                return segmentData;
            }
            finally
            {
                try { File.Delete(tempRawPath); } catch { }
            }
        }

        private byte[] CreateDummyFrame()
        {
            int size = TARGET_WIDTH * TARGET_HEIGHT * 4;
            byte[] dummy = new byte[size];
            for (int i = 0; i < dummy.Length; i += 4)
            {
                dummy[i + 2] = 255;
                dummy[i + 3] = 255;
            }
            return dummy;
        }

        private static async Task<byte[]> ReadStreamToEndAsync(Stream stream)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }

        private void CleanOldSegments(int keepLast)
        {
            var keys = mcolSegmentsInMemory.Keys.OrderByDescending(k => k).Skip(keepLast).ToList();
            foreach (var key in keys)
                mcolSegmentsInMemory.TryRemove(key, out _);
        }

        public byte[]? GetSegment(int segmentId)
        {
            mcolSegmentsInMemory.TryGetValue(segmentId, out var data);
            return data;
        }

        public int GetSegmentCount() => mcolSegmentsInMemory.Count;

        public int GetNextSegmentId() => mvarNextSegmentId;

        public void Dispose()
        {
            mvarIsRunning = false;
            mcolSegmentsInMemory.Clear();
        }
    }
}
