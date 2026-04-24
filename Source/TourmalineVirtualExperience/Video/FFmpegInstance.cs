using System.Diagnostics;

namespace TourmalineVirtualExperience.Video
{
    public class FFmpegInstance : IDisposable
    {
        private readonly string mvarPath;

        public FFmpegInstance(string ffmpegPath)
        {
            mvarPath = ffmpegPath;
        }

        public async Task<byte[]> GenerateSegmentAsync(byte[]? frameData, float durationSeconds)
        {
            string arguments = $"-f rawvideo -pix_fmt bgra -s 800x600 -r 20 -i - " +
                               $"-c:v libx264 -preset veryfast -tune zerolatency " +
                               $"-b:v 1500k -maxrate 2500k -bufsize 5000k " +
                               $"-pix_fmt yuv420p -g 30 -bf 0 " +
                               $"-f mpegts -t {durationSeconds} pipe:1 -loglevel debug";


            var startInfo = new ProcessStartInfo
            {
                FileName = mvarPath,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true, // Redirigimos stderr
                UseShellExecute = false,
                CreateNoWindow = true
            };


            using var process = Process.Start(startInfo);
            if (process == null)
                throw new Exception("No se pudo iniciar FFmpeg");

            // Captura y muestra stderr de ffmpeg en consola
            var errorTask = Task.Run(async () =>
            {
                using var reader = process.StandardError;
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    Console.WriteLine($"[ffmpeg] {line}");
                }
            });

            byte[] frameToSend = frameData ?? CreateDummyFrame();


            using (var input = process.StandardInput.BaseStream)
            {
                int totalFrames = (int)(durationSeconds * 20);
                for (int f = 0; f < totalFrames; f++)
                {
                    await input.WriteAsync(frameToSend, 0, frameToSend.Length);
                    if (f % 10 == 0)
                        await Task.Delay(1);
                }
            }


            process.StandardInput.Close();

            byte[] segmentData = await ReadStreamToEndAsync(process.StandardOutput.BaseStream);
            await process.WaitForExitAsync(new CancellationTokenSource(10000).Token);
            await errorTask; // Espera a que termine la lectura de stderr

            return segmentData;
        }

        private byte[] CreateDummyFrame()
        {
            int size = 800 * 600 * 4;
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

        public void Dispose() { }
    }
}
