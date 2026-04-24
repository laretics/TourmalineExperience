using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orts.Viewer3D
{
    class TourmalineFrameSender:IDisposable
    {
        private readonly string mvarPipeName = "TourmalineFramePipe";

        public async Task SendFrameAsync(byte[] frameData)
        {
            try
            {
                using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", mvarPipeName, PipeDirection.Out))
                {
                    await pipeClient.ConnectAsync(3000);
                    using (BinaryWriter writer = new BinaryWriter(pipeClient))
                    {
                        writer.Write(frameData.Length);
                        writer.Write(frameData);
                        await pipeClient.FlushAsync();

                        //Console.WriteLine($"[TourmalineFrameSender] Frame enviado: {width}x{height} - {frameData.Length / 1024} KB");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TourmalineFrameSender] Error enviando frame: {ex.Message}");
            }
        }

        public void Dispose(){}
    }
}
