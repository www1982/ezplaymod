using HarmonyLib;
using EZPlay.Core;
using EZPlay.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace EZPlay.Patches
{
    [HarmonyPatch(typeof(Repairable.States), "InitializeStates")]
    public static class BuildingBrokenPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Repairable.States __instance)
        {
            __instance.repaired.EventTransition(GameHashes.BuildingReceivedDamage, __instance.allowed, smi =>
            {
                var building = smi.master.gameObject;
                var payload = new
                {
                    buildingId = building.GetComponent<KPrefabID>().InstanceID.ToString(),
                    buildingName = building.GetProperName(),
                    cell = Grid.PosToCell(building.transform.position)
                };
                _eventBroadcaster?.BroadcastEvent("Alert.Building.Broken", payload);
                return smi.NeedsRepairs();
            });
        }
    }

    [HarmonyPatch(typeof(Deconstructable), "OnDeconstructComplete")]
    public static class BuildingDeconstructedPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Deconstructable __instance, WorkerBase worker)
        {
            var building = __instance.gameObject;
            var salvaged = new Dictionary<string, float>();

            // This part is a bit tricky as the original materials are gone.
            // A more robust solution might need to patch another method to get materials before deconstruction.
            // For now, we send an empty object.

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

    [HarmonyPatch(typeof(Storage), "OnStorageChanged")]
    public static class StorageContentChangedPatch
    {
        private static readonly IEventBroadcaster _eventBroadcaster = ServiceContainer.Resolve<IEventBroadcaster>();

        public static void Postfix(Storage __instance, object data)
        {
            var changedItems = __instance.GetItems().Select(item => new
            {
                tag = item.tag.ToString(),
                amount = item.GetComponent<PrimaryElement>().Mass
            }).ToList();

            var payload = new
            {
                storageId = __instance.GetComponent<KPrefabID>().InstanceID.ToString(),
                changedItems = changedItems
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