using HarmonyLib;
using EZPlay.Core;
using EZPlay.Core.Interfaces;

namespace EZPlay.Patches
{
    // Drives the GameStateManager tick for state updates and WebSocket broadcasts.
    [HarmonyPatch(typeof(Game), "Update")]
    public class GameStateMonitorPatch
    {
        public static void Postfix()
        {
            try
            {
                // This is called every frame by the game.
                // GameStateManager.Tick handles its own timing for updates and broadcasts.
                var gameStateManager = ServiceContainer.Resolve<IGameStateManager>();
                gameStateManager?.Tick();
            }
            catch (System.Exception)
            {
                // Silently ignore - services may not be fully initialized yet
            }
        }
    }
}