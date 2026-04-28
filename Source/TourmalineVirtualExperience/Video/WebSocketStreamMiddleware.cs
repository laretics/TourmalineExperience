using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace TourmalineVirtualExperience.Video
{
    public class WebSocketStreamMiddleware
    {
        private readonly RequestDelegate mvarNext;
        private static readonly ConcurrentBag<WebSocket> mcolClients = new();

        public WebSocketStreamMiddleware(RequestDelegate next)
        {
            mvarNext = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            Console.WriteLine($"[WebSocketMiddleware] Path: {context.Request.Path}, IsWebSocket: {context.WebSockets.IsWebSocketRequest}");
            if (context.Request.Path == "/stream" && context.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                mcolClients.Add(webSocket);

                var buffer = new byte[1024 * 1024];
                try
                {
                    while (webSocket.State == WebSocketState.Open)
                    {
                        try
                        {
                            var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                            if (result.MessageType == WebSocketMessageType.Close)
                                break;

                            // Reenviar a todos los clientes menos al emisor
                            foreach (var client in mcolClients.Where(c => c != webSocket && c.State == WebSocketState.Open))
                            {
                                await client.SendAsync(
                                    new ArraySegment<byte>(buffer, 0, result.Count),
                                    WebSocketMessageType.Binary,
                                    result.EndOfMessage,
                                    CancellationToken.None
                                );
                            }
                        }
                        catch (WebSocketException ex)
                        {
                            Console.WriteLine($"[WebSocketMiddleware] WebSocketException: {ex.Message}");
                            break; // Sal del bucle y limpia
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WebSocketMiddleware] Exception: {ex.Message}");
                            break;
                        }
                    }
                }
                finally
                {
                    mcolClients.TryTake(out _);
                    if(webSocket.State== WebSocketState.Open
                    || webSocket.State == WebSocketState.CloseReceived
                    || webSocket.State == WebSocketState.CloseSent)
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                }
            }
            else
            {
                await mvarNext(context);
            }
        }
    }
}
