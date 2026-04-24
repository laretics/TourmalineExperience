using System.IO.Pipes;

namespace TourmalineVirtualExperience.Video
{
    //HeadPoint del canal de envío de frames desde el simulador al generador de segmentos.
    public class SimulatorFrameReceiver
    {
        private readonly string mvarPipeName = "TourmalineFramePipe";
        private NamedPipeServerStream? mvarPipeServer;
        private bool mvarIsRunning = false;
        private Task? mvarListenerTask;

        public event Action<byte[]>? FrameReceived; //FrameData, Width, Height.
        public void Start()
        {
            if(mvarIsRunning) return; //Servidor ya en marcha.

            mvarIsRunning = true;
            mvarListenerTask = Task.Run(ListenerLoop);
            Console.WriteLine("[FrameReceiver] Iniciado - Esperando frames del simulador");
        }

        private async Task ListenerLoop()
        {
            while(mvarIsRunning)
            {
                try
                {
                    mvarPipeServer = new NamedPipeServerStream(mvarPipeName, PipeDirection.In,1, PipeTransmissionMode.Message);
                    await mvarPipeServer.WaitForConnectionAsync();

                    Console.WriteLine("[FrameReceiver] Cliente (simulador) conectado");

                    using BinaryReader reader = new BinaryReader(mvarPipeServer);

                    while(mvarIsRunning && mvarPipeServer.IsConnected)
                    {
                        try
                        {
                            int dataLength = reader.ReadInt32();

                            if (dataLength <= 0 || dataLength > 10_000_000)
                                break; //Protección básica contra datos corruptos.

                            byte[] frameData = reader.ReadBytes(dataLength);

                            FrameReceived?.Invoke(frameData);
                        }
                        catch (EndOfStreamException)
                        {
                            break;  //El simulador cerró la conexión tras enviar un frame normal.
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[FrameReceiver] Error: {ex.Message}");
                            break;
                        }
                    }
                }
                catch(Exception ex)
                {
                    if(mvarIsRunning)
                        Console.WriteLine($"[FrameReceiver] Error en listener: {ex.Message}");
                }
                finally
                {
                    mvarPipeServer?.Dispose();
                    mvarPipeServer = null;
                }
                await Task.Delay(50); //Después de esta pausa intentaremos volver a esperar conexión.
            }
        }

        public void Dispose()
        {
            mvarIsRunning = false;
            mvarPipeServer?.Dispose();
        }
    }
}
