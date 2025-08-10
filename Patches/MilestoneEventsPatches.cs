using HarmonyLib;
using EZPlay.Core;
using EZPlay.Core.Interfaces;
using System.Linq;

namespace EZPlay.Patches
{
    [HarmonyPatch(typeof(Immigration), "OnNewImmigrantsAvailable")]
    public static class NewPrintablesAvailablePatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Immigration __instance)
        {
            var carePackagesField = typeof(Immigration).GetField("carePackages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var carePackages = (System.Collections.Generic.List<CarePackageInfo>)carePackagesField.GetValue(__instance);
            var choices = carePackages.Select(p => new { type = p.id, details = p.requirement }).ToList();

            var payload = new
            {
                choices = choices
            };
            _eventBroadcaster?.BroadcastEvent("Milestone.PrintingPod.NewPrintablesAvailable", payload);
        }
    }

    [HarmonyPatch(typeof(ArtifactAnalysisStation), "OnAnalyzeComplete")]
    public static class ArtifactAnalyzedPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(ArtifactAnalysisStation __instance, SpaceArtifact artifact)
        {
            var payload = new
            {
                artifactId = artifact.GetComponent<KPrefabID>().PrefabTag.ToString(),
                techPointsGained = artifact.artifactTier.payloadDropChance,
                discoveredLore = ""
            };
            _eventBroadcaster?.BroadcastEvent("Milestone.Artifact.Analyzed", payload);
        }
    }

    [HarmonyPatch(typeof(Schedule), "Changed")]
    public static class ScheduleChangedPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Schedule __instance)
        {
            var payload = new
            {
                scheduleId = __instance.name,
                blocks = __instance.GetBlocks().Select(b => new
                {
                    name = b.name,
                    allowed_types = b.allowed_types.Select(t => t.Id).ToList()
                }).ToList()
            };
            _eventBroadcaster?.BroadcastEvent("StateChange.Schedule.Changed", payload);
        }
    }
}