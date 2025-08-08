using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading;

namespace EZPlay.API
{
    public class EventSocketServer
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

        public void BroadcastEvent(object eventData)
        {
            var json = JsonConvert.SerializeObject(eventData);
            _server.WebSocketServices["/"].Sessions.Broadcast(json);
        }
    }

    public class EventSocketBehaviour : WebSocketBehavior
    {
        public void Broadcast(string data)
        {
            Sessions.Broadcast(data);
        }
    }
}