using HarmonyLib;
using EZPlay.Core;
using EZPlay.Core.Interfaces;

namespace EZPlay.Patches
{
    // 当有新的可打印项目时触发
    [HarmonyPatch(typeof(Immigration), "Sim200ms")]
    public static class NewPrintablesAvailablePatch
    {
        private static IEventBroadcaster _eventBroadcaster => ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Prefix(Immigration __instance, out bool __state)
        {
            __state = __instance.ImmigrantsAvailable;
        }

        public static void Postfix(Immigration __instance, bool __state)
        {
            if (!__state && __instance.ImmigrantsAvailable)
            {
                _eventBroadcaster?.BroadcastEvent("Milestone.NewPrintablesAvailable", new { });
            }
        }
    }
}