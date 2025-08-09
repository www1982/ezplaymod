using HarmonyLib;
using EZPlay.Core;
using EZPlay.Core.Interfaces;
using UnityEngine;

namespace EZPlay.Patches
{
    // 捕获复制人死亡事件
    [HarmonyPatch(typeof(DeathMonitor.Instance), "Kill")]
    public class DuplicantDeathPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(DeathMonitor.Instance __instance, Death death)
        {
            // 确保是复制人死亡
            if (!__instance.IsDuplicant) return;

            var victimName = __instance.gameObject.GetProperName();
            var deathReason = death.Id;

            string eventType = null;
            if (deathReason == "Suffocation")
            {
                eventType = "Alert.DuplicantSuffocating";
            }
            else if (deathReason == "Starvation")
            {
                eventType = "Alert.DuplicantStarving";
            }

            if (eventType != null)
            {
                _eventBroadcaster?.BroadcastEvent(eventType, new { DuplicantName = victimName });
            }
        }
    }

    // 捕获建筑过热事件
    [HarmonyPatch(typeof(BuildingHP), "OnOverheating")]
    public class BuildingOverheatingPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(BuildingHP __instance)
        {
            var building = __instance.gameObject;
            var position = building.transform.position;
            _eventBroadcaster?.BroadcastEvent("Alert.BuildingOverheating", new
            {
                BuildingName = building.GetProperName(),
                Position = new { X = position.x, Y = position.y }
            });
        }
    }
}