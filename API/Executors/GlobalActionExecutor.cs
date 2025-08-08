using System;
using System.Collections.Generic;
using UnityEngine;

namespace EZPlay.API.Executors
{
    /// <summary>
    /// 负责处理不针对特定游戏对象的全局指令，如建造、挖掘、研究等。
    /// </summary>
    public static class GlobalActionExecutor
    {
        // 1. 建造能力 (修正版, 返回GameObject)
        public static GameObject Build(string buildingId, int cell, Orientation orientation = Orientation.Neutral, List<Tag> selected_elements = null)
        {
            var def = Assets.GetBuildingDef(buildingId);
            if (def == null)
                throw new ArgumentException($"Building with ID '{buildingId}' not found.");

            var pos = Grid.CellToPosCBC(cell, def.SceneLayer);
            if (selected_elements == null)
            {
                selected_elements = def.DefaultElements();
            }
            if (orientation == Orientation.Neutral)
            {
                orientation = def.InitialOrientation;
            }

            // 直接调用BuildingDef的TryPlace方法，绕过UI工具
            var go = def.TryPlace(null, pos, orientation, selected_elements, null, 0);

            if (go == null)
            {
                // 尝试作为替换来建造
                var replacementCandidate = def.GetReplacementCandidate(cell);
                if (replacementCandidate != null)
                {
                    go = def.TryReplaceTile(null, pos, orientation, selected_elements, null, 0);
                }
            }

            if (go == null)
                throw new Exception($"Failed to place building '{buildingId}' at cell {cell}. Location may be invalid or obstructed.");

            // 设置默认优先级
            var prioritizable = go.GetComponent<Prioritizable>();
            if (prioritizable != null)
            {
                prioritizable.SetMasterPriority(new PrioritySetting(PriorityScreen.PriorityClass.basic, 5));
            }
            return go;
        }

        // 2. 挖掘能力 (修正版)
        public static void Dig(List<int> cells)
        {
            if (cells == null || cells.Count == 0)
                throw new ArgumentException("'cells' list cannot be null or empty.");

            // 直接调用挖掘动作，而不是工具本身
            foreach (var cell in cells)
            {
                InterfaceTool.ActiveConfig.DigAction.Dig(cell, 0);
            }
        }

        // 3. 研究能力 (修正版)
        public static void ManageResearch(string techId, bool cancel = false)
        {
            var research = Research.Instance;
            if (research == null)
                throw new Exception("Research instance not found.");

            if (cancel)
            {
                var activeResearch = research.GetActiveResearch();
                if (activeResearch != null)
                {
                    // 修正：CancelResearch需要一个Tech参数
                    research.CancelResearch(activeResearch.tech);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(techId))
                    throw new ArgumentException("'techId' must be provided to set research.");

                var tech = Db.Get().Techs.Get(techId);
                if (tech == null)
                    throw new ArgumentException($"Tech with ID '{techId}' not found.");

                research.SetActiveResearch(tech, true); // clearQueue=true to match player behavior
            }
        }

        // 4. 辅助工具：坐标转换 (修正版)
        public static int CoordsToCell(int x, int y)
        {
            int cell = Grid.XYToCell(x, y);
            // 修正：Grid没有IsValidXY，但我们可以通过检查cell是否有效来达到同样目的
            if (!Grid.IsValidCell(cell))
                throw new ArgumentException($"Invalid coordinates: ({x}, {y})");
            return cell;
        }

        // 5. 预检：建造
        public static bool CanBuild(string buildingId, int cell)
        {
            var def = Assets.GetBuildingDef(buildingId);
            if (def == null) return false;

            var orientation = def.InitialOrientation;
            string fail_reason;

            // 使用游戏内置的函数来检查是否可以建造
            return def.IsValidPlaceLocation(null, Grid.CellToPos(cell), orientation, out fail_reason);
        }

        // 6. 预检：挖掘
        public static Dictionary<int, bool> CanDig(List<int> cells)
        {
            var result = new Dictionary<int, bool>();
            if (cells == null) return result;

            foreach (var cell in cells)
            {
                if (!Grid.IsValidCell(cell) || !Grid.Solid[cell])
                {
                    result[cell] = false;
                    continue;
                }

                var go = Grid.Objects[cell, (int)ObjectLayer.FoundationTile];
                result[cell] = go != null && go.GetComponent<Diggable>() != null;
            }
            return result;
        }
    }
}