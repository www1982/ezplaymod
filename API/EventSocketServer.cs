using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading;
using EZPlay.Core;
using EZPlay.API.Models;
using EZPlay.GameState;
using EZPlay.Core.Interfaces;

namespace EZPlay.API
{
    public class EventSocketServer : IEventBroadcaster
    {
        private readonly WebSocketServer _server;

        public EventSocketServer(string url)
        {
            _server = new WebSocketServer(url);
            _server.AddWebSocketService<EventSocketBehaviour>("/");
        }

        public void Start()
        {
            var thread = new Thread(() =>
            {
                _server.Start();
            })
            {
                IsBackground = true
            };
            thread.Start();
        }

        public void Stop()
        {
            _server.Stop();
        }

        public void BroadcastEvent(string eventType, object payload)
        {
            var gameEvent = new GameEvent
            {
                EventType = eventType,
                Cycle = GameClock.Instance.GetCycle(),
                Payload = payload
            };

            var json = JsonConvert.SerializeObject(gameEvent);
            _server.WebSocketServices["/"].Sessions.Broadcast(json);
        }

        public void Broadcast(string message)
        {
            _server.WebSocketServices["/"].Sessions.Broadcast(message);
        }
    }

    public class EventSocketBehaviour : WebSocketBehavior
    {
        protected override void OnOpen()
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

        public void Broadcast(string data)
        {
            Sessions.Broadcast(data);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var request = JsonConvert.DeserializeObject<ApiRequest>(e.Data);
            var handler = new RequestHandler();
            var response = handler.HandleRequest(request);
            Send(JsonConvert.SerializeObject(response));
        }
    }

}