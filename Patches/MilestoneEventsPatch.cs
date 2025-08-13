using HarmonyLib;
using EZPlay.Core;
using System.Collections.Generic;
using EZPlay.API;
using System.Linq;
using UnityEngine;
using TUNING;
using Klei.AI;

namespace EZPlay.Patches
{
    // 捕获新复制人打印事件
    //[HarmonyPatch(typeof(Immigration), "ApplyDefaultPersonalPriorities")]
    public class NewDuplicantPatch
    {
        public static void Postfix(GameObject minion)
        {
            var identity = minion.GetComponent<MinionIdentity>();
            var traitsComponent = identity.GetComponent<Traits>();
            var traits = traitsComponent.GetTraitIds();

            EZPlay.Core.ServiceContainer.Resolve<EventSocketServer>().BroadcastEvent("Milestone.NewDuplicantPrinted", new
            {
                Name = identity.GetProperName(),
                Traits = traits
            });
        }
    }

    // 当有新的可打印项目时触发
    //[HarmonyPatch(typeof(Immigration), "Sim200ms")]
    public static class NewPrintablesAvailablePatch
    {
        public static void Prefix(Immigration __instance, out bool __state)
        {
            __state = __instance.ImmigrantsAvailable;
        }



        public static void Postfix(Immigration __instance, bool __state)
        {
            if (!__state && __instance.ImmigrantsAvailable)
            {
                EZPlay.Core.ServiceContainer.Resolve<EventSocketServer>().BroadcastEvent("Milestone.NewPrintablesAvailable", new { });
            }
        }
    }

    //[HarmonyPatch(typeof(Research), "CheckBuyResearch")]
    public class Research_CheckBuyResearch_Patch
    {
        public static void Postfix(Tech tech)
        {
            if (tech == null) return;

            EZPlay.Core.ServiceContainer.Resolve<EventSocketServer>().BroadcastEvent("ResearchComplete", new
            {
                TechId = tech.Id,
                TechName = tech.Name
            });
        }
    }
}