using System;
using System.Collections.Generic;
using System.Reflection;
using EZPlay.API.Executors;
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

                    // For now, we assume the element is available. A more robust implementation would check.
                    var selectedElements = new List<Tag> { item.Element.CreateTag() };

                    GameObject buildGhost = GlobalActionExecutor.Build(item.PrefabID, targetCell, item.Orientation, selectedElements);
                    if (buildGhost != null)
                    {
                        ApplySettings(buildGhost, item.Settings);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BlueprintPlacer] Failed to place item '{item.PrefabID}': {ex.Message}");
                }
            }
        }

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

                    // This is a simplified setting application. It assumes all settings are properties.
                    // A full implementation would need to differentiate between properties and methods like in ReflectionExecutor.
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