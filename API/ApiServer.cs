using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using EZPlay.API.Exceptions;
using EZPlay.API.Models;
using EZPlay.GameState;
using EZPlay.Core;
using EZPlay.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        private static readonly EZPlay.Core.Logger Logger = ServiceLocator.Resolve<EZPlay.Core.Logger>();
        protected override void OnOpen()
        {
            Console.WriteLine($"[EZPlay.GameService] Client connected: {ID}");

            var versionPayload = new JObject
            {
                ["version"] = ModLoader.ApiVersion
            };
            var versionMessage = new JObject
            {
                ["type"] = "Api.Version",
                ["payload"] = versionPayload
            };
            Send(versionMessage.ToString());

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
                var errorResponse = new ApiResponse
                {
                    Type = "Error",
                    Status = "error",
                    Payload = "Unauthorized IP address."
                };
                Send(JsonConvert.SerializeObject(errorResponse));
                Context.WebSocket.Close();
                return;
            }

            string requestId = null;
            try
            {
                var request = JsonConvert.DeserializeObject<ApiRequest>(e.Data);
                if (request == null || string.IsNullOrEmpty(request.Action))
                {
                    throw new ApiException(400, "Invalid request format. 'Action' is required.");
                }

                // Store requestId for use in the continuation
                requestId = request.RequestId;

                var task = MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var handler = new RequestHandler();
                    // We pass the original request object to the handler
                    return handler.HandleRequest(request);
                });

                task.ContinueWith(t =>
                {
                    ApiResponse response;
                    if (t.IsFaulted)
                    {
                        var ex = t.Exception.InnerException ?? t.Exception;
                        Logger.Error($"Error handling action '{request.Action}': {ex.Message}\n{ex.StackTrace}");
                        response = new ApiResponse
                        {
                            Type = request.Action + ".Error",
                            Status = "error",
                            Payload = new { message = ex.Message, stackTrace = ex.StackTrace },
                            RequestId = requestId // Use the stored requestId
                        };
                    }
                    else
                    {
                        // The handler now returns a complete ApiResponse
                        response = t.Result;
                        // Ensure the correct response type and status are set for success
                        response.Type = request.Action + ".Response";
                        response.Status = "success";
                        response.RequestId = requestId; // Use the stored requestId
                    }
                    Send(JsonConvert.SerializeObject(response));
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse request: {ex.Message}\n{e.Data}");
                // Try to extract requestId from the raw JSON for error reporting
                try
                {
                    var jObject = JObject.Parse(e.Data);
                    requestId = jObject.Value<string>("requestId");
                }
                catch
                {
                    // Ignore if parsing fails, requestId will be null
                }

                var errorResponse = new ApiResponse
                {
                    Type = "Request.ParseError",
                    Status = "error",
                    Payload = new { message = "Failed to parse incoming request.", error = ex.Message },
                    RequestId = requestId
                };
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

}