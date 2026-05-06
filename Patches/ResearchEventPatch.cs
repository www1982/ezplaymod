using HarmonyLib;
using EZPlay.Core;
using EZPlay.Core.Interfaces;

namespace EZPlay.Patches
{
    [HarmonyPatch(typeof(Research), "CompleteResearch")]
    public class Research_CompleteResearch_Patch
    {
        public static void Postfix(Tech tech)
        {
            ServiceContainer.Resolve<IEventBroadcaster>()?.BroadcastEvent("ResearchComplete", new
            {
                TechId = tech.Id,
                TechName = tech.Name
            });
        }
    }
}
