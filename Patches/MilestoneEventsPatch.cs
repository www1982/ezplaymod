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
    // 当有新的可打印项目时触发
    [HarmonyPatch(typeof(Immigration), "Sim200ms")]
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

}