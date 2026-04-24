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

        // Array para permitir escalar fácilmente a 3 o más instancias
        private readonly FFmpegInstance?[] mcolInstances;
        private int mvarActiveInstanceIndex = 0;

        public VideoSegmentGenerator(int maxInstances = 2)
        {
            mcolInstances = new FFmpegInstance?[maxInstances];

            mvarffMpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "ffmpeg.exe");
            if (!File.Exists(mvarffMpegPath))
                throw new FileNotFoundException($"No se encontró ffmpeg.exe en: {mvarffMpegPath}");

            Console.WriteLine($"[VideoSegmentGenerator] FFmpeg encontrado en: {mvarffMpegPath}");
        }

        public void EnqueueFrame(byte[] frameData)
        {
            if (frameData == null || frameData.Length == 0) return;
            mvarCurrentFrameBuffer = (byte[])frameData.Clone();
        }

        public void EnsureStarted()
        {
            if (mvarIsRunning) return;
            mvarIsRunning = true;
            Task.Run(async () => await GenerationLoop());
            Console.WriteLine("[VideoSegmentGenerator] Generación con múltiples instancias iniciada");
        }

        private async Task GenerationLoop()
        {
            while (mvarIsRunning)
            {
                try
                {
                    float segmentDuration = 5.0f; // ← Luego variable según aceleración

                    byte[] segmentData = await GenerateWithCurrentInstanceAsync(segmentDuration);

                    int segmentId = mvarNextSegmentId++;
                    mcolSegmentsInMemory[segmentId] = segmentData;

                    Console.WriteLine($"[VideoSegment] Segmento {segmentId} generado - {segmentData.Length / 1024} KB");

                    CleanOldSegments(8);
                    await Task.Delay((int)(segmentDuration * 1000) - 800);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[VideoSegment] Error generando segmento: {ex.Message}");
                    await Task.Delay(1000);
                }

                // Rotamos a la siguiente instancia
                mvarActiveInstanceIndex = (mvarActiveInstanceIndex + 1) % mcolInstances.Length;
            }
        }

        private async Task<byte[]> GenerateWithCurrentInstanceAsync(float durationSeconds)
        {
            int index = mvarActiveInstanceIndex;

            if (null==mcolInstances[index])
                mcolInstances[index] = new FFmpegInstance(mvarffMpegPath);

            return await mcolInstances[index]!.GenerateSegmentAsync(mvarCurrentFrameBuffer, durationSeconds);
        }

        private void CleanOldSegments(int keepLast)
        {
            var keysToRemove = mcolSegmentsInMemory.Keys
                .OrderByDescending(k => k)
                .Skip(keepLast)
                .ToList();

            foreach (var key in keysToRemove)
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
            foreach (var instance in mcolInstances)
                instance?.Dispose();
            mcolSegmentsInMemory.Clear();
        }
    }
}
