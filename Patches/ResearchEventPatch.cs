using HarmonyLib;
using EZPlay.Core;

using EZPlay.API;
namespace EZPlay.Patches
{
    [HarmonyPatch(typeof(Research), "CompleteResearch")]
    public class Research_CompleteResearch_Patch
    {
        public static void Postfix(Tech tech)
        {
            EZPlay.Core.ServiceContainer.Resolve<EventSocketServer>().BroadcastEvent("ResearchComplete", new
            {
                TechId = tech.Id,
                TechName = tech.Name
            });
        }
    }
}