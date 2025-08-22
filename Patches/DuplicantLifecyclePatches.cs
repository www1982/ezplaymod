using HarmonyLib;
using EZPlay.Core;
using EZPlay.Core.Interfaces;
using System.Linq;
using Klei.AI;
using System.Collections.Generic;

namespace EZPlay.Patches
{
    [HarmonyPatch(typeof(Telepad), "OnAcceptDelivery")]
    public static class DuplicantPrintedPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();
        private static MinionStartingStats printedStats;
        private static readonly object _lock = new object();

        public static void Prefix(ITelepadDeliverable deliverable)
        {
            lock (_lock)
            {
                if (deliverable is MinionStartingStats stats)
                {
                    printedStats = stats;
                }
            }
        }

        public static void Postfix()
        {
            lock (_lock)
            {
                if (printedStats == null) return;

                var minion = Components.LiveMinionIdentities.Items.FirstOrDefault(m =>
                    m.GetComponent<Traits>().GetTraitIds().SequenceEqual(printedStats.Traits.Select(t => t.Id)));

                if (minion == null) return;

                var payload = new
                {
                    duplicantId = minion.GetComponent<KPrefabID>().InstanceID.ToString(),
                    duplicantName = minion.GetProperName(),
                    traits = minion.GetComponent<Traits>().GetTraitIds()
                };

                _eventBroadcaster?.BroadcastEvent("Lifecycle.Duplicant.Printed", payload);

                printedStats = null;
            }
        }
    }

    [HarmonyPatch(typeof(DeathMonitor.Instance), "Kill")]
    public static class DuplicantDeathPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(DeathMonitor.Instance __instance, Death death)
        {
            if (!__instance.IsDuplicant) return;

            var minionIdentity = __instance.GetComponent<MinionIdentity>();
            if (minionIdentity == null) return;

            var victimName = __instance.gameObject.GetProperName();
            var deathReason = death.Id;

            var deathPayload = new
            {
                duplicantId = minionIdentity.GetComponent<KPrefabID>().InstanceID.ToString(),
                duplicantName = victimName,
                causeOfDeath = deathReason,
                cell = Grid.PosToCell(minionIdentity.transform.position)
            };
            _eventBroadcaster?.BroadcastEvent("Lifecycle.Duplicant.Death", deathPayload);
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
        private static readonly HashSet<string> StressTraitIds = new HashSet<string> { "Vomiter", "Destructive", "BingeEater", "UglyCrier", "Banshee" };

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
                breakType = minionIdentity.GetComponent<Traits>().GetTraitIds().FirstOrDefault(t => StressTraitIds.Contains(t)) ?? "Unknown"
            };

            _eventBroadcaster?.BroadcastEvent("Alert.Duplicant.StressBreak", payload);
        }
    }

    [HarmonyPatch(typeof(Sicknesses), "CreateInstance")]
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
    [HarmonyPatch(typeof(Klei.AI.AttributeInstance), "SetValue")]
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