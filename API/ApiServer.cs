using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using EZPlay.GameState;
using EZPlay.Utils;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace EZPlay.API
{
    public static class ApiServer
    {
        private static WebSocketServer _server;
        private static Thread _serverThread;
        // Simple IP whitelist for security
        private static readonly List<IPAddress> AllowedIPs = new List<IPAddress>
        {
            IPAddress.Parse("127.0.0.1"),
            IPAddress.Loopback,
            IPAddress.IPv6Loopback
        };

        public static void Start()
        {
            if (_server != null && _server.IsListening)
            {
                return;
            }

            _serverThread = new Thread(() =>
            {
                _server = new WebSocketServer("ws://0.0.0.0:8080");
                _server.AddWebSocketService<GameService>("/api");
                _server.Start();

                Console.WriteLine("[EZPlay.ApiServer] WebSocket Server started on ws://0.0.0.0:8080/api");
            });
            _serverThread.IsBackground = true;
            _serverThread.Start();
        }

        public static void Stop()
        {
            if (_server != null && _server.IsListening)
            {
                _server.Stop();
                _server = null;
            }

            if (_serverThread != null && _serverThread.IsAlive)
            {
                _serverThread.Abort();
                _serverThread = null;
            }
        }

        public static void Broadcast(string message)
        {
            if (_server != null && _server.IsListening)
            {
                _server.WebSocketServices["/api"].Sessions.Broadcast(message);
            }
        }

        public static bool IsClientAllowed(IPAddress ipAddress)
        {
            return AllowedIPs.Contains(ipAddress);
        }
    }

    public class GameService : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            Console.WriteLine($"[EZPlay.GameService] Client connected: {ID}");
            // Send initial state
            var initialState = GameStateManager.LastKnownState;
            var response = new { type = "GameState", payload = initialState };
            Send(JsonConvert.SerializeObject(response));
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if (!ApiServer.IsClientAllowed(Context.UserEndPoint.Address))
            {
                Console.WriteLine($"[EZPlay.GameService] Unauthorized request from {Context.UserEndPoint.Address}");
                var errorResponse = new { type = "Error", payload = "Unauthorized IP address." };
                Send(JsonConvert.SerializeObject(errorResponse));
                Context.WebSocket.Close();
                return;
            }

            try
            {
                var request = JsonConvert.DeserializeObject<ApiRequest>(e.Data);
                if (request == null || string.IsNullOrEmpty(request.Action))
                {
                    throw new ArgumentException("Invalid request format.");
                }

                // Use the dispatcher to run the logic on the main game thread
                var task = MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var handler = new RequestHandler();
                    return handler.HandleRequest(request.Action, request.Payload);
                });

                // Asynchronously wait for the result and send it back
                task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        var errorResponse = new { type = "Error", payload = t.Exception.InnerException.Message };
                        Send(JsonConvert.SerializeObject(errorResponse));
                    }
                    else
                    {
                        var response = new { type = request.Action + "Response", payload = t.Result };
                        Send(JsonConvert.SerializeObject(response));
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EZPlay.GameService] Error processing request: {ex.Message}");
                var errorResponse = new { type = "Error", payload = ex.Message };
                Send(JsonConvert.SerializeObject(errorResponse));
            }
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Console.WriteLine($"[EZPlay.GameService] Client disconnected: {ID}");
        }

        protected override void OnError(ErrorEventArgs e)
        {
            Console.WriteLine($"[EZPlay.GameService] Error: {e.Message}");
        }
    }

    // Helper class to deserialize requests
    public class ApiRequest
    {
        public string Action { get; set; }
        public object Payload { get; set; }
    }
}