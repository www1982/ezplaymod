using System;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EZPlay.Core;
using EZPlay.API.Models;
using EZPlay.GameState;
using EZPlay.Core.Interfaces;

namespace EZPlay.API
{
    public class EventSocketServer : IEventBroadcaster
    {
        private readonly WebSocketServer _server;
        private volatile bool _isRunning = false;

        public EventSocketServer(string url)
        {
            _server = new WebSocketServer(url);
            _server.AddWebSocketService<EventSocketBehaviour>("/");
        }

        public void Start()
        {
            var thread = new Thread(() =>
            {
                try
                {
                    _server.Start();
                    _isRunning = true;
                    Console.WriteLine("[EZPlay.EventSocketServer] Event server started.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EZPlay.EventSocketServer] Failed to start: {ex.Message}");
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        public void Stop()
        {
            try
            {
                _isRunning = false;
                _server?.Stop();
            }
            catch (Exception)
            {
                // Ignore shutdown errors
            }
        }

        public void BroadcastEvent(string eventType, object payload)
        {
            if (!_isRunning) return;
            try
            {
                var gameEvent = new GameEvent
                {
                    EventType = eventType,
                    Cycle = GameClock.Instance != null ? GameClock.Instance.GetCycle() : -1,
                    Payload = payload
                };

                var json = JsonConvert.SerializeObject(gameEvent);
                _server.WebSocketServices["/"].Sessions.Broadcast(json);
            }
            catch (Exception)
            {
                // Silently ignore broadcast failures (e.g. no clients connected, server not ready)
            }
        }

        public void Broadcast(string message)
        {
            if (!_isRunning) return;
            try
            {
                _server.WebSocketServices["/"].Sessions.Broadcast(message);
            }
            catch (Exception)
            {
                // Silently ignore
            }
        }
    }

    public class EventSocketBehaviour : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            try
            {
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
            }
            catch (Exception)
            {
                // Ignore
            }
        }

        public void Broadcast(string data)
        {
            Sessions.Broadcast(data);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                var request = JsonConvert.DeserializeObject<ApiRequest>(e.Data);
                var handler = new RequestHandler();
                var response = handler.HandleRequest(request);
                Send(JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EZPlay.EventSocketBehaviour] Error handling message: {ex.Message}");
            }
        }
    }

}