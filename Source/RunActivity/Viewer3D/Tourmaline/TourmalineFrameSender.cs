using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Orts.Viewer3D.Tourmaline
{
    class TourmalineFrameSender:IDisposable
    {
        private readonly BlockingCollection<byte[]> mcolFrames;
        private readonly int MAX_LENGTH = 10; //Máximo de fotogramas que vamos a encolar, para evitar lag.
        private ClientWebSocket mvarSocket; //Canal de transmisión.
        private readonly Uri mvarUri;
        private readonly CancellationTokenSource mvarCancellationToken = new CancellationTokenSource();
        private Task mvarSenderTask;

        public TourmalineFrameSender(string url)
        {
            mcolFrames = new BlockingCollection<byte[]>(MAX_LENGTH);
            mvarUri = new Uri(url);
        }
        public void Start()
        {
            mvarSenderTask = Task.Run(SenderLoopAsync);
        }
        public void EnqueueFrame(byte[] rhs)
        {
            //Mientras la cola esté llena vamos descartando frames viejos.
            while (!mcolFrames.TryAdd(rhs))
                mcolFrames.TryTake(out _);
        }

        private async Task SenderLoopAsync()
        {
            while (!mvarCancellationToken.IsCancellationRequested)
            {
                try
                {
                    if(mcolFrames.Count>0)
                    {
                        if (null != mvarSocket && mvarSocket.State != WebSocketState.Open)
                        {
                            mvarSocket.Dispose();
                            mvarSocket = null;
                        }
                        if(null==mvarSocket)
                        {
                            mvarSocket = new ClientWebSocket();
                            await mvarSocket.ConnectAsync(mvarUri, mvarCancellationToken.Token);
                        }
                        byte[] auxFrame = mcolFrames.Take(mvarCancellationToken.Token);                        
                        await mvarSocket.SendAsync(new ArraySegment<byte>(auxFrame), WebSocketMessageType.Binary, true, mvarCancellationToken.Token);
                        //Console.WriteLine($"[TourmalineFrameSender] Frame sent {DateTime.Now}");
                    }
                }
                catch(OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    //Esperamos un poco y reintentamos
                    await Task.Delay(500, mvarCancellationToken.Token);
                }
            }
        }
        public void Dispose()
        {
            mvarCancellationToken.Cancel();
            if (null != mvarSenderTask)
            {
                try
                {
                    mvarSenderTask.Wait();
                }
                catch (AggregateException ex)
                {
                    // Ignorar TaskCanceledException
                    ex.Handle(e => e is TaskCanceledException || e is OperationCanceledException);
                }
            }
            if (null!=mvarSocket)
                mvarSocket.Dispose();
            mcolFrames.Dispose();            
        }
    }
}
