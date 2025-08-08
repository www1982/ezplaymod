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

            ModLoader.EventServer.BroadcastEvent("DuplicantDeath", new
            {
                DuplicantName = __instance.name
            });
        }
    }
}