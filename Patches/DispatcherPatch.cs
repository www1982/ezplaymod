using HarmonyLib;
using EZPlay.Core;
using EZPlay.API;
using EZPlay.Utils;
using UnityEngine;

namespace EZPlay.Patches
{
    // 每一帧都处理来自主线程调度器的任务队列
    [HarmonyPatch(typeof(Game), "Update")]
    public class DispatcherPatch
    {
        private static float lastBroadcastTime = 0f;
        private const float BROADCAST_INTERVAL = 1f; // 每秒广播一次

        public static void Postfix()
        {
            MainThreadDispatcher.ProcessQueue();

            if (Time.time - lastBroadcastTime >= BROADCAST_INTERVAL)
            {
                EZPlay.Core.ServiceContainer.Resolve<EventSocketServer>().BroadcastEvent(
                    "Simulation.Tick",
                    new
                    {
                        game_time_in_seconds = GameClock.Instance.GetTime(),
                        cycle = GameClock.Instance.GetCycle(),
                        is_paused = SpeedControlScreen.Instance.IsPaused
                    }
                );
                lastBroadcastTime = Time.time;
            }
        }
    }
}