using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using EZPlay.API.Exceptions;
using EZPlay.API.Models;
using EZPlay.GameState;
using EZPlay.Core;
using EZPlay.Core.Interfaces;
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
        private static readonly ISecurityWhitelist SecurityWhitelist = ServiceContainer.Resolve<ISecurityWhitelist>();

        public static void Start(int port = 8080)
        {
            if (_server != null && _server.IsListening)
            {
                return;
            }

            _serverThread = new Thread(() =>
            {
                _server = new WebSocketServer($"ws://0.0.0.0:{port}");
                _server.AddWebSocketService<GameService>("/api");
                _server.Start();

                Console.WriteLine($"[EZPlay.ApiServer] WebSocket Server started on ws://0.0.0.0:{port}/api");
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
            return SecurityWhitelist.IsIPAllowed(ipAddress);
        }
    }

    public class GameService : WebSocketBehavior
    {
        private static readonly EZPlay.Core.Logger Logger = EZPlay.Core.ServiceContainer.Resolve<EZPlay.Core.Logger>();
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
                Logger.Warning($"Unauthorized access attempt from IP: {Context.UserEndPoint.Address}");
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

            ApiRequest request = null;
            try
            {
                request = JsonConvert.DeserializeObject<ApiRequest>(e.Data);
                if (request == null || string.IsNullOrEmpty(request.Action))
                {
                    throw new ApiException(400, "Invalid request format. 'Action' is required.");
                }

                var requestId = request.RequestId;

                var task = MainThreadDispatcher.RunOnMainThread(() =>
                {
                    var handler = new RequestHandler();
                    return handler.HandleRequest(request);
                });

                task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        var ex = t.Exception?.InnerException ?? t.Exception;
                        Logger.Error($"Error handling action '{request.Action}': {ex.Message}\n{ex.StackTrace}");
                        var errorResponse = ApiResponse.Error(request.Action, ex.Message, ex.StackTrace, requestId);
                        Send(JsonConvert.SerializeObject(errorResponse));
                    }
                    else
                    {
                        var response = t.Result;
                        response.RequestId = requestId;
                        Send(JsonConvert.SerializeObject(response));
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse request: {ex.Message}\n{e.Data}");
                string requestId = null;
                try
                {
                    var jObject = JObject.Parse(e.Data);
                    requestId = jObject.Value<string>("requestId");
                }
                catch
                {
                    // Ignore if parsing fails
                }

                var errorResponse = ApiResponse.ParseError(ex.Message, requestId);
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