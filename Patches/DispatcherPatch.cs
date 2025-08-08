using HarmonyLib;
using EZPlay.Utils;

namespace EZPlay.Patches
{
    // 每一帧都处理来自主线程调度器的任务队列
    [HarmonyPatch(typeof(Game), "Update")]
    public class DispatcherPatch
    {
        public static void Postfix()
        {
            MainThreadDispatcher.ProcessQueue();
        }
    }
}