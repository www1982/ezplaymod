using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// =========================================================================
// VIII. 蓝图系统 - 管理器
// =========================================================================

public static class BlueprintManager
{
    private static readonly string BlueprintsFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "Blueprints");

    static BlueprintManager()
    {
        // Ensure the blueprints directory exists
        Directory.CreateDirectory(BlueprintsFolderPath);
    }

    public static Blueprint CreateBlueprint(string name, List<string> objectIds, string anchorObjectId)
    {
        var anchorGo = GameObjectManager.GetObject(anchorObjectId);
        if (anchorGo == null) throw new ArgumentException($"Anchor object with ID '{anchorObjectId}' not found.");

        var anchorPos = Grid.PosToXY(anchorGo.transform.position);

        var blueprint = new Blueprint
        {
            Name = name,
            Author = "AI Agent",
            Buildings = new List<BlueprintItem>(),
            Tiles = new List<BlueprintItem>(),
            Others = new List<BlueprintItem>()
        };

        int minX = anchorPos.x, minY = anchorPos.y, maxX = anchorPos.x, maxY = anchorPos.y;

        foreach (var objectId in objectIds)
        {
            var go = GameObjectManager.GetObject(objectId);
            if (go == null) continue;

            var kpid = go.GetComponent<KPrefabID>();
            var primaryElement = go.GetComponent<PrimaryElement>();
            var rotatable = go.GetComponent<Rotatable>();
            var building = go.GetComponent<Building>();

            if (kpid == null) continue;

            var currentPos = Grid.PosToXY(go.transform.position);
            var offset = new Vector2I(currentPos.x - anchorPos.x, currentPos.y - anchorPos.y);

            minX = Math.Min(minX, currentPos.x);
            minY = Math.Min(minY, currentPos.y);
            maxX = Math.Max(maxX, currentPos.x);
            maxY = Math.Max(maxY, currentPos.y);

            var item = new BlueprintItem
            {
                PrefabID = kpid.PrefabTag.ToString(),
                Offset = offset,
                Element = primaryElement?.ElementID ?? SimHashes.Void,
                Orientation = rotatable?.GetOrientation() ?? Orientation.Neutral,
                Settings = ExtractSettings(go)
            };

            if (building != null)
            {
                blueprint.Buildings.Add(item);
            }
            else if (go.GetComponent<Wire>() != null || go.GetComponent<LiquidConduit>() != null || go.GetComponent<GasConduit>() != null)
            {
                blueprint.Tiles.Add(item);
            }
            else
            {
                blueprint.Others.Add(item);
            }
        }

        blueprint.Dimensions = new Vector2I(maxX - minX + 1, maxY - minY + 1);
        return blueprint;
    }

    private static Dictionary<string, JToken> ExtractSettings(GameObject go)
    {
        var settings = new Dictionary<string, JToken>();
        foreach (var componentName in SecurityWhitelist.AllowedComponents)
        {
            var component = go.GetComponent(componentName);
            if (component == null) continue;

            var properties = component.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var propInfo in properties)
            {
                string fullPropName = $"{componentName}.{propInfo.Name}";
                if (SecurityWhitelist.AllowedProperties.Contains(fullPropName))
                {
                    try
                    {
                        object value = propInfo.GetValue(component, null);
                        settings[fullPropName] = JToken.FromObject(value);
                    }
                    catch { }
                }
            }
        }
        return settings;
    }

    public static void SaveBlueprintToFile(Blueprint blueprint)
    {
        string filePath = Path.Combine(BlueprintsFolderPath, $"{blueprint.Name}.json");
        string json = JsonConvert.SerializeObject(blueprint, Formatting.Indented);
        File.WriteAllText(filePath, json);
    }

    public static Blueprint LoadBlueprintFromFile(string name)
    {
        string filePath = Path.Combine(BlueprintsFolderPath, $"{name}.json");
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Blueprint '{name}' not found at '{filePath}'.");
        }
        string json = File.ReadAllText(filePath);
        return JsonConvert.DeserializeObject<Blueprint>(json);
    }
}
