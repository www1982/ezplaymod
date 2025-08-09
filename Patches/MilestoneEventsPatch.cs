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
    [HarmonyPatch(typeof(Research), "CompleteResearch")]
    public class ResearchCompletePatch
    {
        public static void Postfix(Research __instance, Tech tech)
        {
            ServiceLocator.Resolve<EventSocketServer>().BroadcastEvent("Milestone.ResearchComplete", new
            {
                TechId = tech.Id,
                TechName = tech.Name
            });
        }
    }

    // 捕获新复制人打印事件
    [HarmonyPatch(typeof(Immigration), "OnSpawn")]
    public class NewDuplicantPatch
    {
        public static void Postfix(Immigration __instance, GameObject duplicant)
        {
            var identity = duplicant.GetComponent<MinionIdentity>();
            var traitsComponent = identity.GetComponent<Traits>();
            var traits = traitsComponent.GetTraitIds();

            ServiceLocator.Resolve<EventSocketServer>().BroadcastEvent("Milestone.NewDuplicantPrinted", new
            {
                Name = identity.GetProperName(),
                Traits = traits
            });
        }
    }
}