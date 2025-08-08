using HarmonyLib;
using EZPlay.Core;

namespace EZPlay.Patches
{
    [HarmonyPatch(typeof(MinionIdentity), "OnDeath")]
    public class MinionIdentity_OnDeath_Patch
    {
        public static void Postfix(MinionIdentity __instance)
        {
            if (ModLoader.EventServer == null) return;

            var eventData = new
            {
                EventType = "DuplicantDeath",
                DuplicantName = __instance.name
            };
            ModLoader.EventServer.BroadcastEvent(eventData);
        }
    }
}