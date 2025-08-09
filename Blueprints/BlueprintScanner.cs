using System.Collections.Generic;
using UnityEngine;

namespace EZPlay.Blueprints
{
    /// <summary>
    /// 提供了扫描游戏世界区域并将其转换为蓝图数据结构的功能。
    /// </summary>
    public static class BlueprintScanner
    {
        // 定义需要扫描的对象图层
        private static readonly int[] ObjectLayers =
        {
            (int)ObjectLayer.Building,
            (int)ObjectLayer.GasConduit,
            (int)ObjectLayer.LiquidConduit,
            (int)ObjectLayer.SolidConduit,
            (int)ObjectLayer.Wire,
            (int)ObjectLayer.LogicWire,
            (int)ObjectLayer.FoundationTile
        };

        /// <summary>
        /// 扫描指定的矩形区域，并将所有发现的对象转换为一个蓝图。
        /// </summary>
        /// <param name="name">要创建的蓝图的名称。</param>
        /// <param name="area">要扫描的游戏世界区域。</param>
        /// <returns>一个包含扫描区域内所有对象信息的蓝图对象。</returns>
        public static Blueprint ScanAreaToBlueprint(string name, Rect area)
        {
            var blueprint = new Blueprint
            {
                Name = name,
                Dimensions = new Vector2I(Mathf.RoundToInt(area.width), Mathf.RoundToInt(area.height))
            };

            var areaMin = new Vector2I(Mathf.RoundToInt(area.xMin), Mathf.RoundToInt(area.yMin));

            // 遍历区域内的每一个单元格
            for (int y = 0; y < blueprint.Dimensions.y; y++)
            {
                for (int x = 0; x < blueprint.Dimensions.x; x++)
                {
                    var cellOffset = new Vector2I(x, y);
                    int cell = Grid.XYToCell(areaMin.x + x, areaMin.y + y);

                    if (!Grid.IsValidCell(cell)) continue;

                    ScanCellForObjects(cell, cellOffset, blueprint);
                }
            }

            return blueprint;
        }

        /// <summary>
        /// 扫描单个单元格中的所有指定图层，并将找到的对象添加到蓝图中。
        /// </summary>
        private static void ScanCellForObjects(int cell, Vector2I offset, Blueprint blueprint)
        {
            foreach (int layer in ObjectLayers)
            {
                GameObject gameObject = Grid.Objects[cell, layer];
                if (gameObject == null) continue;

                // 确保我们只处理每个对象一次（基于其主建筑组件）
                var building = gameObject.GetComponent<Building>();
                if (building != null && Grid.PosToCell(building.transform.GetPosition()) != cell)
                {
                    continue; // 这不是该建筑的主单元格，跳过以防重复
                }

                var item = CreateItemFromGameObject(gameObject, offset);
                if (item == null) continue;

                var def = Assets.GetBuildingDef(item.PrefabID);
                if (def == null) continue;

                // 根据对象的类型将其分类到不同的列表中
                if (def.IsTilePiece)
                {
                    blueprint.Tiles.Add(item);
                }
                else
                {
                    blueprint.Buildings.Add(item);
                }
            }
        }

        /// <summary>
        /// 从游戏对象中提取信息，创建一个可序列化的 BlueprintItem。
        /// </summary>
        private static BlueprintItem CreateItemFromGameObject(GameObject go, Vector2I offset)
        {
            var buildingComplete = go.GetComponent<BuildingComplete>();
            if (buildingComplete == null) return null; // 只处理已完成的建筑

            var primaryElement = go.GetComponent<PrimaryElement>();
            if (primaryElement == null) return null;

            var item = new BlueprintItem
            {
                PrefabID = buildingComplete.Def.PrefabID,
                Offset = offset,
                Element = primaryElement.ElementID,
                Orientation = Orientation.Neutral, // 默认值
                Settings = new Dictionary<string, Newtonsoft.Json.Linq.JToken>() // 暂不实现设置的提取
            };

            // 尝试获取方向信息
            var rotatable = go.GetComponent<Rotatable>();
            if (rotatable != null)
            {
                item.Orientation = rotatable.GetOrientation();
            }

            // TODO: 在此添加逻辑以提取不同建筑的特定设置 (e.g., Storage, Logic Ports)
            // 例如:
            // var storage = go.GetComponent<Storage>();
            // if (storage != null)
            // {
            //     item.Settings["storage_capacity"] = storage.capacityKg;
            // }

            return item;
        }
    }
}