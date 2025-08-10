using HarmonyLib;
using EZPlay.Core;
using EZPlay.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EZPlay.Patches
{
    [HarmonyPatch(typeof(Repairable), "OnSpawn")]
    public static class BuildingBrokenPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Repairable __instance)
        {
            var controller = __instance.GetComponent<StateMachineController>();
            if (controller != null)
            {
                var smi = controller.GetSMI<Repairable.SMInstance>();
                if (smi != null)
                {
                    var sm = smi.sm;
                    sm.repaired.EventTransition(GameHashes.BuildingReceivedDamage, sm.allowed, event_smi =>
                    {
                        var building = event_smi.master.gameObject;
                        var payload = new
                        {
                            buildingId = building.GetComponent<KPrefabID>().InstanceID.ToString(),
                            buildingName = building.GetProperName(),
                            cell = Grid.PosToCell(building.transform.position)
                        };
                        _eventBroadcaster?.BroadcastEvent("Alert.Building.Broken", payload);
                        return event_smi.NeedsRepairs();
                    });
                }
            }
        }
    }

    [HarmonyPatch(typeof(Deconstructable), "OnCompleteWork")]
    public static class BuildingDeconstructedPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Deconstructable __instance, WorkerBase worker)
        {
            var building = __instance.gameObject;
            var salvaged = new Dictionary<string, float>();

            // Note: A more robust solution might need to patch another method 
            // to get materials before deconstruction. For now, we send an empty object.

            var payload = new
            {
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
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Storage __instance, GameObject go)
        {
            var payload = new
            {
                storageId = __instance.GetComponent<KPrefabID>().InstanceID.ToString(),
                changedItems = new[] { new {
                    tag = go.PrefabID().ToString(),
                    amount = go.GetComponent<PrimaryElement>().Mass
                }}
            };
            _eventBroadcaster?.BroadcastEvent("StateChange.Storage.ContentChanged", payload);
        }
    }

    [HarmonyPatch(typeof(Storage), "Remove")]
    public static class StorageRemovePatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Storage __instance, GameObject go)
        {
            var payload = new
            {
                storageId = __instance.GetComponent<KPrefabID>().InstanceID.ToString(),
                changedItems = new[] { new {
                    tag = go.PrefabID().ToString(),
                    amount = -go.GetComponent<PrimaryElement>().Mass // Negative amount for removal
                }}
            };
            _eventBroadcaster?.BroadcastEvent("StateChange.Storage.ContentChanged", payload);
        }
    }

    [HarmonyPatch(typeof(Overheatable.States), nameof(Overheatable.States.overheated), MethodType.Getter)]
    public static class BuildingOverheatingPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(StateMachine.BaseState __result)
        {
            __result.enterActions.Add(new StateMachine.Action("BroadcastOverheatEvent", (System.Action<StateMachine.Instance>)(smi =>
            {
                var overheatable = smi.GetComponent<Overheatable>();
                if (overheatable == null) return;

                var building = overheatable.gameObject;
                var temperatureMonitor = building.GetComponent<PrimaryElement>();

                var payload = new
                {
                    buildingId = building.GetComponent<KPrefabID>().InstanceID.ToString(),
                    buildingName = building.GetProperName(),
                    cell = Grid.PosToCell(building.transform.position),
                    currentTemp = temperatureMonitor?.Temperature,
                    overheatTemp = overheatable.OverheatTemperature
                };

                _eventBroadcaster?.BroadcastEvent("Alert.Building.Overheated", payload);
            })));
        }
    }
}