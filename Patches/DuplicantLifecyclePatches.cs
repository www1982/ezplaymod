using HarmonyLib;
using EZPlay.Core;
using EZPlay.Core.Interfaces;
using System.Linq;
using Klei.AI;

namespace EZPlay.Patches
{
    [HarmonyPatch(typeof(CharacterSelectionController), "OnProceed")]
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

    [HarmonyPatch(typeof(DeathMonitor.Instance), "Kill")]
    public static class DuplicantDeathPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(DeathMonitor.Instance __instance, Death cause)
        {
            var minionIdentity = __instance.GetComponent<MinionIdentity>();
            if (minionIdentity == null) return;

            var payload = new
            {
                duplicantId = minionIdentity.GetComponent<KPrefabID>().InstanceID.ToString(),
                duplicantName = minionIdentity.GetProperName(),
                causeOfDeath = cause.Id,
                cell = Grid.PosToCell(minionIdentity.transform.position)
            };

            _eventBroadcaster?.BroadcastEvent("Lifecycle.Duplicant.Death", payload);
        }
    }

    [HarmonyPatch(typeof(MinionResume), "MasterSkill")]
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

    [HarmonyPatch(typeof(StressMonitor.Instance), "HasHadEnough")]
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

    [HarmonyPatch(typeof(Sicknesses), "Add")]
    public static class DuplicantDiseaseGainedPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Sicknesses __instance, SicknessInstance sicknessInstance)
        {
            var minionIdentity = __instance.GetComponent<MinionIdentity>();
            if (minionIdentity == null) return;

            var payload = new
            {
                duplicantId = minionIdentity.GetComponent<KPrefabID>().InstanceID.ToString(),
                duplicantName = minionIdentity.GetProperName(),
                diseaseId = sicknessInstance.Sickness.Id
            };

            _eventBroadcaster?.BroadcastEvent("Alert.Dulicant.DiseaseGained", payload);
        }
    }
}