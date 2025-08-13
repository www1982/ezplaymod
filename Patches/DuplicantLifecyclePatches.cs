using HarmonyLib;
using EZPlay.Core;
using EZPlay.Core.Interfaces;
using System.Linq;
using Klei.AI;

namespace EZPlay.Patches
{
    ////[HarmonyPatch(typeof(CharacterSelectionController), "OnProceed")]
    public static class DuplicantPrintedPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(CharacterSelectionController __instance)
        {
            var selectedDeliverablesField = typeof(CharacterSelectionController).GetField("selectedDeliverables", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var selectedDeliverables = (System.Collections.Generic.List<ITelepadDeliverable>)selectedDeliverablesField.GetValue(__instance);

            if (selectedDeliverables == null || selectedDeliverables.Count == 0) return;

            foreach (var deliverable in selectedDeliverables)
            {
                if (deliverable is MinionStartingStats minionStats)
                {
                    // This is a bit of a hack, as we don't have a direct reference to the spawned minion.
                    // We'll find the most recently created minion and assume it's the one we just printed.
                    var minion = Components.LiveMinionIdentities.Items.OrderByDescending(m => m.arrivalTime).FirstOrDefault();
                    if (minion == null) return;

                    var payload = new
                    {
                        duplicantId = minion.GetComponent<KPrefabID>().InstanceID.ToString(),
                        duplicantName = minion.GetProperName(),
                        traits = minion.GetComponent<Traits>().GetTraitIds()
                    };

                    _eventBroadcaster?.BroadcastEvent("Lifecycle.Duplicant.Printed", payload);
                }
            }
        }
    }

    //[HarmonyPatch(typeof(DeathMonitor.Instance), "Kill")]
    public static class DuplicantDeathPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(DeathMonitor.Instance __instance, Death death)
        {
            // 确保是复制人死亡
            if (!__instance.IsDuplicant) return;

            var minionIdentity = __instance.GetComponent<MinionIdentity>();
            if (minionIdentity == null) return;

            var victimName = __instance.gameObject.GetProperName();
            var deathReason = death.Id;

            // 广播通用的死亡事件
            var deathPayload = new
            {
                duplicantId = minionIdentity.GetComponent<KPrefabID>().InstanceID.ToString(),
                duplicantName = victimName,
                causeOfDeath = deathReason,
                cell = Grid.PosToCell(minionIdentity.transform.position)
            };
            _eventBroadcaster?.BroadcastEvent("Lifecycle.Duplicant.Death", deathPayload);

            // 广播特定的警报事件
            string alertEventType = null;
            if (deathReason == "Suffocation")
            {
                alertEventType = "Alert.DuplicantSuffocating";
            }
            else if (deathReason == "Starvation")
            {
                alertEventType = "Alert.DuplicantStarving";
            }

            if (alertEventType != null)
            {
                _eventBroadcaster?.BroadcastEvent(alertEventType, new { DuplicantName = victimName });
            }
        }
    }

    //[HarmonyPatch(typeof(MinionResume), "MasterSkill")]
    public static class DuplicantGainedSkillPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(MinionResume __instance, string skillId)
        {
            var minionIdentity = __instance.GetComponent<MinionIdentity>();
            if (minionIdentity == null) return;

            var payload = new
            {
                duplicantId = minionIdentity.GetComponent<KPrefabID>().InstanceID.ToString(),
                duplicantName = minionIdentity.GetProperName(),
                skillId = skillId
            };

            _eventBroadcaster?.BroadcastEvent("Lifecycle.Duplicant.GainedSkill", payload);
        }
    }

    //[HarmonyPatch(typeof(StressMonitor.Instance), "HasHadEnough")]
    public static class DuplicantStressBreakPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(StressMonitor.Instance __instance, ref bool __result)
        {
            if (!__result) return;

            var minionIdentity = __instance.master.GetComponent<MinionIdentity>();
            if (minionIdentity == null) return;

            var payload = new
            {
                duplicantId = minionIdentity.GetComponent<KPrefabID>().InstanceID.ToString(),
                duplicantName = minionIdentity.GetProperName(),
                stressPercent = __instance.stress.value,
                breakType = "BingeEat" // This is an assumption
            };

            _eventBroadcaster?.BroadcastEvent("Alert.Duplicant.StressBreak", payload);
        }
    }

    //[HarmonyPatch(typeof(Sicknesses), "CreateInstance")]
    public static class DuplicantDiseaseGainedPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Sicknesses __instance, ref SicknessInstance __result)
        {
            var minionIdentity = __instance.GetComponent<MinionIdentity>();
            if (minionIdentity == null) return;

            var payload = new
            {
                duplicantId = minionIdentity.GetComponent<KPrefabID>().InstanceID.ToString(),
                duplicantName = minionIdentity.GetProperName(),
                diseaseId = __result.Sickness.Id
            };

            _eventBroadcaster?.BroadcastEvent("Alert.Dulicant.DiseaseGained", payload);
        }
    }
    ////[HarmonyPatch(typeof(Klei.AI.AttributeInstance), "SetValue")]
    public static class DuplicantAttributeChangedPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Klei.AI.AttributeInstance __instance, float value)
        {
            var minionIdentity = __instance.gameObject.GetComponent<MinionIdentity>();
            if (minionIdentity == null) return;

            var payload = new
            {
                duplicantId = minionIdentity.GetComponent<KPrefabID>().InstanceID.ToString(),
                duplicantName = minionIdentity.GetProperName(),
                attributeId = __instance.Attribute.Id,
                attributeName = __instance.Attribute.Name,
                newValue = value
            };

            _eventBroadcaster?.BroadcastEvent("Lifecycle.Duplicant.AttributeChanged", payload);
        }
    }
}