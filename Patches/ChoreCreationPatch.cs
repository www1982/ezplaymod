using HarmonyLib;
using UnityEngine;
using EZPlay.Blueprints;

namespace EZPlay.Patches
{
    /// <summary>
    /// 对 GlobalChoreProvider.AddChore 应用补丁，以捕获所有新创建的 Chore。
    /// </summary>
    [HarmonyPatch(typeof(GlobalChoreProvider), "AddChore")]
    public static class ChoreCreation_Patch
    {
        /// <summary>
        /// Postfix 补丁，在 AddChore 方法执行后运行。
        /// </summary>
        public static void Postfix(Chore chore)
        {
            if (chore == null) return;

            // 尝试获取 Chore 的目标 GameObject
            GameObject targetObject = chore.target?.gameObject;
            if (targetObject == null) return;

            // 获取目标单元格
            int cell = Grid.PosToCell(targetObject);
            if (cell == Grid.InvalidCell) return;

            // 通知 BlueprintPlacer 有新的 Chore 被创建
            BlueprintPlacer.NotifyChoreCreated(chore, cell);
        }
    }
}