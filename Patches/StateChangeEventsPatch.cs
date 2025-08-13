using HarmonyLib;
using EZPlay.Core;
using EZPlay.Core.Interfaces;
using UnityEngine;

namespace EZPlay.Patches
{
    // 捕获间歇泉状态变更事件
    //[HarmonyPatch(typeof(Geyser), "OnStateChanged")]
    public class GeyserStateChangePatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Geyser __instance, Geyser.States old_state, Geyser.States new_state)
        {
            var position = __instance.transform.position;
            _eventBroadcaster?.BroadcastEvent("StateChange.GeyserStateChanged", new
            {
                GeyserName = __instance.GetProperName(),
                Position = new { X = position.x, Y = position.y },
                OldState = old_state.ToString(),
                NewState = new_state.ToString()
            });
        }
    }
}