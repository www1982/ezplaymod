using HarmonyLib;
using EZPlay.Core;
using EZPlay.Core.Interfaces;

namespace EZPlay.Patches
{
    [HarmonyPatch(typeof(MinionIdentity), "OnDied")]
    public class MinionIdentity_OnDeath_Patch
    {
        private static IEventBroadcaster _eventBroadcaster => ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(MinionIdentity __instance)
        {
            _eventBroadcaster?.BroadcastEvent("DuplicantDeath", new
            {
                worldId = __instance.GetMyWorldId(),
                DuplicantName = __instance.name
            });
        }
    }
}