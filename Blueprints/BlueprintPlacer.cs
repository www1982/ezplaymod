using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EZPlay.Blueprints
{
    public static class BlueprintPlacer
    {
        public static void PlaceBlueprint(Blueprint blueprint, int anchorCell)
        {
            var allItems = new List<BlueprintItem>();
            allItems.AddRange(blueprint.Buildings);
            allItems.AddRange(blueprint.Tiles);
            allItems.AddRange(blueprint.Others);

            foreach (var item in allItems)
            {
                try
                {
                    int targetCell = Grid.OffsetCell(anchorCell, new CellOffset(item.Offset.x, item.Offset.y));
                    if (!Grid.IsValidCell(targetCell)) continue;

                    var buildingDef = Assets.GetBuildingDef(item.PrefabID);
                    if (buildingDef == null) continue;

                    // Set orientation before placing
                    if (BuildTool.Instance != null)
                    {
                        BuildTool.Instance.SetToolOrientation(item.Orientation);

                        // Try to place the building
                        var element = ElementLoader.FindElementByHash(item.Element);
                        //float temperature = (element != null) ? element.defaultValues.temperature : TUNING.BUILDINGS.PLANSUFFICIENTMASS;
                        GameObject placedBuilding = buildingDef.Build(targetCell, item.Orientation, null, new List<Tag> { item.Element.CreateTag() }, 300f, true, -1f);
                        if (placedBuilding != null)
                        {
                            // Applying settings after placement is complex and might require waiting for the building to be constructed.
                            // For now, we will skip applying settings post-placement.
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BlueprintPlacer] Failed to place item '{item.PrefabID}': {ex.Message}");
                }
            }
        }

        // ApplySettings is kept for potential future use but is not called in the current implementation.
        private static void ApplySettings(GameObject go, Dictionary<string, JToken> settings)
        {
            foreach (var setting in settings)
            {
                try
                {
                    string[] parts = setting.Key.Split('.');
                    if (parts.Length != 2) continue;

                    string componentName = parts[0];
                    string propName = parts[1];

                    var component = go.GetComponent(componentName);
                    if (component == null) continue;

                    var propInfo = component.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                    if (propInfo != null && propInfo.CanWrite)
                    {
                        object value = setting.Value.ToObject(propInfo.PropertyType);
                        propInfo.SetValue(component, value, null);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BlueprintPlacer] Failed to apply setting '{setting.Key}': {ex.Message}");
                }
            }
        }
    }
}