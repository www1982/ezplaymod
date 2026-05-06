using HarmonyLib;
using EZPlay.Core;
using EZPlay.Core.Interfaces;
using EZPlay.Utils;
using UnityEngine;

namespace EZPlay.Patches
{
    // 每一帧都处理来自主线程调度器的任务队列
    [HarmonyPatch(typeof(Game), "Update")]
    public class DispatcherPatch
    {
        private static IEventBroadcaster _eventBroadcaster => ServiceContainer.Resolve<IEventBroadcaster>();
        private static float lastBroadcastTime = 0f;
        private const float BROADCAST_INTERVAL = 1f; // 每秒广播一次

        public static void Postfix()
        {
            MainThreadDispatcher.ProcessQueue();

            if (Time.time - lastBroadcastTime >= BROADCAST_INTERVAL)
            {
                try
                {
                    if (GameClock.Instance != null && SpeedControlScreen.Instance != null)
                    {
                        _eventBroadcaster?.BroadcastEvent(
                            "Simulation.Tick",
                            new
                            {
                                game_time_in_seconds = GameClock.Instance.GetTime(),
                                cycle = GameClock.Instance.GetCycle(),
                                is_paused = SpeedControlScreen.Instance.IsPaused
                            }
                        );
                    }
                }
                catch (System.Exception)
                {
                    // Silently ignore - game state may not be ready yet
                }
                lastBroadcastTime = Time.time;
            }
        }
    }
}