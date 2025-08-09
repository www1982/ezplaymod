using HarmonyLib;
using EZPlay.GameState;
using EZPlay.Core;
using EZPlay.API;
using Newtonsoft.Json.Linq;

namespace EZPlay.Patches
{
    // Drives the GameStateManager tick for state updates and WebSocket broadcasts.
    [HarmonyPatch(typeof(Game), "Update")]
    public class GameStateMonitorPatch
    {
        public static void Postfix()
        {
            // This is called every frame by the game.
            // GameStateManager.Tick handles its own timing for updates and broadcasts.
            GameStateManager.Tick();

            var eventServer = ServiceLocator.Resolve<EventSocketServer>();
            if (eventServer == null) return;

            var tickPayload = new JObject
            {
                ["game_time"] = GameClock.Instance.GetTime()
            };
            var tickMessage = new JObject
            {
                ["type"] = "Simulation.Tick",
                ["payload"] = tickPayload
            };

            eventServer.Broadcast(tickMessage.ToString());
        }
    }
}