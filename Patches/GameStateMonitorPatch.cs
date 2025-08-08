using HarmonyLib;
using EZPlay.GameState;

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
        }
    }
}