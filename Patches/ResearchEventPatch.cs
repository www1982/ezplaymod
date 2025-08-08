using HarmonyLib;
using EZPlay.Core;

namespace EZPlay.Patches
{
    [HarmonyPatch(typeof(Research), "CompleteResearch")]
    public class Research_CompleteResearch_Patch
    {
        public static void Postfix(Tech tech)
        {
            if (ModLoader.EventServer == null) return;

            ModLoader.EventServer.BroadcastEvent("ResearchComplete", new
            {
                TechId = tech.Id,
                TechName = tech.Name
            });
        }
    }
}