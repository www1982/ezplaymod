using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
        private static volatile bool _isRunning = false;
        private static ISecurityWhitelist SecurityWhitelist => ServiceContainer.Resolve<ISecurityWhitelist>();

        public static void Start(int port = 8080)
        {
            if (_server != null && _server.IsListening)
            {
                return;
            }

            var thread = new Thread(() =>
            {
                try
                {
                    _server = new WebSocketServer($"ws://0.0.0.0:{port}");
                    _server.AddWebSocketService<GameService>("/api");
                    _server.Start();
                    _isRunning = true;

                    Console.WriteLine($"[EZPlay.ApiServer] WebSocket Server started on ws://0.0.0.0:{port}/api");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EZPlay.ApiServer] Failed to start server: {ex.Message}");
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        public static void Stop()
        {
            try
            {
                _isRunning = false;
                if (_server != null && _server.IsListening)
                {
                    _server.Stop();
                    _server = null;
                }
            }
            catch (Exception)
            {
                // Ignore shutdown errors
            }
        }

        public static void Broadcast(string message)
        {
            if (!_isRunning || _server == null || !_server.IsListening) return;
            try
            {
                _server.WebSocketServices["/api"].Sessions.Broadcast(message);
            }
            catch (Exception)
            {
                // Ignore broadcast errors
            }
        }

        public static bool IsClientAllowed(IPAddress ipAddress)
        {
            try
            {
                return SecurityWhitelist?.IsIPAllowed(ipAddress) ?? true;
            }
            catch (Exception)
            {
                return true; // Allow by default if whitelist is unavailable
            }
        }
    }

    public class GameService : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            try
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
                var gameStateManager = ServiceContainer.Resolve<IGameStateManager>();
                if (gameStateManager?.LastKnownState != null)
                {
                    var response = new { type = "GameState", payload = gameStateManager.LastKnownState };
                    Send(JsonConvert.SerializeObject(response));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EZPlay.GameService] Error in OnOpen: {ex.Message}");
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            ApiRequest request = null;
            try
            {
                var whitelist = ServiceContainer.Resolve<ISecurityWhitelist>();
                if (whitelist != null && !whitelist.IsIPAllowed(Context.UserEndPoint.Address))
                {
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
                    try
                    {
                        if (t.IsFaulted)
                        {
                            var ex = t.Exception?.InnerException ?? t.Exception;
                            var errorResponse = ApiResponse.Error(request.Action, ex.Message, ex.StackTrace, requestId);
                            Send(JsonConvert.SerializeObject(errorResponse));
                        }
                        else
                        {
                            var response = t.Result;
                            response.RequestId = requestId;
                            Send(JsonConvert.SerializeObject(response));
                        }
                    }
                    catch (Exception)
                    {
                        // Connection may have been closed
                    }
                });
            }
            catch (Exception ex)
            {
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

                try
                {
                    var errorResponse = ApiResponse.ParseError(ex.Message, requestId);
                    Send(JsonConvert.SerializeObject(errorResponse));
                }
                catch (Exception)
                {
                    // Connection may have been closed
                }
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