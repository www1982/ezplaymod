using HarmonyLib;
using EZPlay.Core;

using EZPlay.API;
namespace EZPlay.Patches
{
    //[HarmonyPatch(typeof(MinionIdentity), "OnDied")]
    public class MinionIdentity_OnDeath_Patch
    {
        public static void Postfix(MinionIdentity __instance)
        {
            EZPlay.Core.ServiceContainer.Resolve<EventSocketServer>().BroadcastEvent("DuplicantDeath", new
            {
                DuplicantName = __instance.name
            });
        }
    }
}