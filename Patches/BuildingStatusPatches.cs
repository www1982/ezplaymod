using HarmonyLib;
using EZPlay.Core;
using EZPlay.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EZPlay.Patches
{
    [HarmonyPatch(typeof(Repairable.States), "InitializeStates")]
    public static class Repairable_InitializeStates_Patch
    {
        private static IEventBroadcaster _eventBroadcaster => ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Repairable.States __instance)
        {
            __instance.allowed.Enter(smi =>
            {
                var building = smi.master.gameObject;
                var payload = new
                {
                    worldId = building.GetMyWorldId(),
                    buildingId = building.GetComponent<KPrefabID>().InstanceID.ToString(),
                    buildingName = building.GetProperName(),
                    cell = Grid.PosToCell(building.transform.position)
                };
                _eventBroadcaster?.BroadcastEvent("Alert.Building.Broken", payload);
            });
        }
    }

    [HarmonyPatch(typeof(Deconstructable), "OnCompleteWork")]
    public static class BuildingDeconstructedPatch
    {
        private static IEventBroadcaster _eventBroadcaster => ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Deconstructable __instance, WorkerBase worker)
        {
            var building = __instance.gameObject;
            var salvaged = new Dictionary<string, float>();

            // Note: A more robust solution might need to patch another method 
            // to get materials before deconstruction. For now, we send an empty object.

            var payload = new
            {
                worldId = building.GetMyWorldId(),
                buildingId = building.GetComponent<KPrefabID>().InstanceID.ToString(),
                buildingName = building.GetProperName(),
                cell = Grid.PosToCell(building.transform.position),
                materialsSalvaged = salvaged
            };

            _eventBroadcaster?.BroadcastEvent("Milestone.Building.Deconstructed", payload);
        }
    }

    [HarmonyPatch(typeof(Storage), "Store")]
    public static class StorageStorePatch
    {
        private static IEventBroadcaster _eventBroadcaster => ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Storage __instance, GameObject go)
        {
            if (__instance == null || go == null) return;
            
            var kPrefabID = go.GetComponent<KPrefabID>();
            var primaryElement = go.GetComponent<PrimaryElement>();
            
            if (kPrefabID == null || primaryElement == null) return;

            var payload = new
            {
                worldId = __instance.GetMyWorldId(),
                storageId = __instance.GetComponent<KPrefabID>()?.InstanceID.ToString() ?? "unknown",
                changedItems = new[] { new {
                    tag = kPrefabID.PrefabTag.ToString(),
                    amount = primaryElement.Mass
                }}
            };
            _eventBroadcaster?.BroadcastEvent("StateChange.Storage.ContentChanged", payload);
        }
    }

    [HarmonyPatch(typeof(Storage), "Remove")]
    public static class StorageRemovePatch
    {
        private static IEventBroadcaster _eventBroadcaster => ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Storage __instance, GameObject go)
        {
            if (__instance == null || go == null) return;

            var kPrefabID = go.GetComponent<KPrefabID>();
            var primaryElement = go.GetComponent<PrimaryElement>();
            
            if (kPrefabID == null || primaryElement == null) return;

            var payload = new
            {
                worldId = __instance.GetMyWorldId(),
                storageId = __instance.GetComponent<KPrefabID>()?.InstanceID.ToString() ?? "unknown",
                changedItems = new[] { new {
                    tag = kPrefabID.PrefabTag.ToString(),
                    amount = -primaryElement.Mass // Negative amount for removal
                }}
            };
            _eventBroadcaster?.BroadcastEvent("StateChange.Storage.ContentChanged", payload);
        }
    }

    [HarmonyPatch(typeof(Overheatable.States), "InitializeStates")]
    public static class BuildingOverheatingPatch
    {
        private static IEventBroadcaster _eventBroadcaster => ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Overheatable.States __instance)
        {
            __instance.overheated.Enter(smi =>
            {
                var overheatable = smi.GetComponent<Overheatable>();
                if (overheatable == null) return;

                var building = overheatable.gameObject;
                var temperatureMonitor = building.GetComponent<PrimaryElement>();

                var payload = new
                {
                    worldId = building.GetMyWorldId(),
                    buildingId = building.GetComponent<KPrefabID>().InstanceID.ToString(),
                    buildingName = building.GetProperName(),
                    cell = Grid.PosToCell(building.transform.position),
                    currentTemp = temperatureMonitor?.Temperature,
                    overheatTemp = overheatable.OverheatTemperature
                };

                _eventBroadcaster?.BroadcastEvent("Alert.Building.Overheated", payload);
            });
        }
    }
}
