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
                geyserId = __instance.GetComponent<KPrefabID>().InstanceID.ToString(),
                geyserType = __instance.GetComponent<KPrefabID>().PrefabTag.ToString(),
                state = "Idle",
                cell = Grid.PosToCell(__instance.transform.position)
            };
            _eventBroadcaster?.BroadcastEvent("StateChange.Geyser.EruptionStateChanged", payload);
        }
    }

    // //[HarmonyPatch(typeof(Game), "Trigger")]
    // public static class NewElementDiscoveredPatch
    // {
    //     private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

    //     public static void Postfix(Game __instance, int hash, object data)
    //     {
    //         if (hash != (int)GameHashes.NewElementDiscovered) return;

    //         if (data is NewElementDiscoveredMessage message)
    //         {
    //             var payload = new
    //             {
    //                 elementId = message.new_element.ToString(),
    //                 cell = message.cell
    //             };
    //             _eventBroadcaster?.BroadcastEvent("Milestone.World.NewElementDiscovered", payload);
    //         }
    //     }
    // }

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