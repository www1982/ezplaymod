using HarmonyLib;
using EZPlay.GameState;

namespace EZPlay.Patches
{
    // 定期更新游戏状态缓存 (用于 /state 端点)
    [HarmonyPatch(typeof(Game), "SimEveryTick")]
    public class GameStateMonitorPatch
    {
        private static float timer = 0f;
        private const float UPDATE_INTERVAL_SECONDS = 2f;
        public static void Postfix(float dt)
        {
            timer += dt;
            if (timer < UPDATE_INTERVAL_SECONDS) return;
            timer = 0f;
            GameStateManager.UpdateState();
        }
    }
}