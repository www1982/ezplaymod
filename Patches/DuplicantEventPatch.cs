using HarmonyLib;
using EZPlay.Core;

using EZPlay.API;
namespace EZPlay.Patches
{
    [HarmonyPatch(typeof(MinionIdentity), "OnDeath")]
    public class MinionIdentity_OnDeath_Patch
    {
        public static void Postfix(MinionIdentity __instance)
        {
            ServiceLocator.Resolve<EventSocketServer>().BroadcastEvent("DuplicantDeath", new
            {
                DuplicantName = __instance.name
            });
        }
    }
}