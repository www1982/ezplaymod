using HarmonyLib;
using EZPlay.Core;
using EZPlay.Core.Interfaces;
using System.Linq;

namespace EZPlay.Patches
{
    [HarmonyPatch(typeof(Geyser), "OnStartErupting")]
    public static class GeyserStartEruptingPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Geyser __instance)
        {
            var payload = new
            {
                worldId = __instance.GetMyWorldId(),
                geyserId = __instance.GetComponent<KPrefabID>().InstanceID.ToString(),
                geyserType = __instance.GetComponent<KPrefabID>().PrefabTag.ToString(),
                state = "Erupting",
                cell = Grid.PosToCell(__instance.transform.position)
            };
            _eventBroadcaster?.BroadcastEvent("StateChange.Geyser.EruptionStateChanged", payload);
        }
    }

    [HarmonyPatch(typeof(Geyser), "OnStopErupting")]
    public static class GeyserStopEruptingPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Geyser __instance)
        {
            var payload = new
            {
                worldId = __instance.GetMyWorldId(),
                geyserId = __instance.GetComponent<KPrefabID>().InstanceID.ToString(),
                geyserType = __instance.GetComponent<KPrefabID>().PrefabTag.ToString(),
                state = "Idle",
                cell = Grid.PosToCell(__instance.transform.position)
            };
            _eventBroadcaster?.BroadcastEvent("StateChange.Geyser.EruptionStateChanged", payload);
        }
    }

    [HarmonyPatch(typeof(DiscoveredResources), "OnDiscover")]
    public static class NewElementDiscoveredPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Tag tag, Tag category)
        {
            var payload = new
            {
                elementId = tag.ToString(),
                category = category.ToString()
            };
            _eventBroadcaster?.BroadcastEvent("Milestone.World.NewElementDiscovered", payload);
        }
    }


    [HarmonyPatch(typeof(Klei.AI.MeteorShowerEvent.States), "TriggerMeteorGlobalEvent")]
    public static class MeteorShowerPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Klei.AI.MeteorShowerEvent.StatesInstance smi, GameHashes hash)
        {
            if (hash != GameHashes.MeteorShowerBombardStateBegins) return;

            var payload = new
            {
                worldId = smi.GetMyWorldId(),
                duration = smi.sm.runTimeRemaining.Get(smi),
                meteors = smi.gameplayEvent.GetMeteorsInfo().Select(m => m.prefab).ToList()
            };
            _eventBroadcaster?.BroadcastEvent("Alert.World.MeteorShower", payload);
        }
    }
}