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

            var eventData = new
            {
                EventType = "ResearchComplete",
                TechId = tech.Id,
                TechName = tech.Name
            };
            ModLoader.EventServer.BroadcastEvent(eventData);
        }
    }
}