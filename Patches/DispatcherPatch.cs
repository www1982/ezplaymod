using HarmonyLib;
using EZPlay.Core;
using EZPlay.API;
using EZPlay.Utils;

namespace EZPlay.Patches
{
    // 每一帧都处理来自主线程调度器的任务队列
    //[HarmonyPatch(typeof(Game), "Update")]
    public class DispatcherPatch
    {
        public static void Postfix()
        {
            MainThreadDispatcher.ProcessQueue();

            EZPlay.Core.ServiceContainer.Resolve<EventSocketServer>().BroadcastEvent(
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
}