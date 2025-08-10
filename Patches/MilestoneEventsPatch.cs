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
    // 捕获研究完成事件
    [HarmonyPatch(typeof(Research), "CheckBuyResearch")]
    public class ResearchCompletePatch
    {
        public static void Postfix(Research __instance)
        {
            if (__instance.GetActiveResearch() == null) return;
            Tech tech = __instance.GetActiveResearch().tech;
            EZPlay.Core.ServiceContainer.Resolve<EventSocketServer>().BroadcastEvent("Milestone.ResearchComplete", new
            {
                TechId = tech.Id,
                TechName = tech.Name
            });
        }
    }

    // 捕获新复制人打印事件
    [HarmonyPatch(typeof(Immigration), "ApplyDefaultPersonalPriorities")]
    public class NewDuplicantPatch
    {
        public static void Postfix(Immigration __instance, GameObject minion)
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
    [HarmonyPatch(typeof(Immigration), "OnNewImmigrantsAvailable")]
    public static class NewPrintablesAvailablePatch
    {
        public static void Postfix()
        {
            EZPlay.Core.ServiceContainer.Resolve<EventSocketServer>().BroadcastEvent("Milestone.NewPrintablesAvailable", null);
        }
    }
}