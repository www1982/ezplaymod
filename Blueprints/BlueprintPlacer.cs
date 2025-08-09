using System;
using System.Collections.Generic;
using EZPlay.Core;
using Klei.AI;
using UnityEngine;

namespace EZPlay.Blueprints
{
    public static class BlueprintPlacer
    {
        // 当一个 Chore 被创建时触发。传递的是 Chore 对象和其目标单元格。
        public static event Action<Chore, int> OnChoreCreated;

        public static void PlaceBlueprint(Blueprint blueprint, int anchorCell)
        {
            var placerGo = new GameObject("BlueprintPlacerInstance");
            var placer = placerGo.AddComponent<BlueprintPlacerInstance>();
            placer.Initialize(blueprint, anchorCell);
        }

        // 允许外部（例如，我们的补丁）触发事件
        public static void NotifyChoreCreated(Chore chore, int cell)
        {
            OnChoreCreated?.Invoke(chore, cell);
        }
    }

    public class BlueprintPlacerInstance : MonoBehaviour
    {
        private static readonly EZPlay.Core.Logger logger = ServiceLocator.Resolve<EZPlay.Core.Logger>();
        private enum DeploymentPhase
        {
            NotStarted,
            PendingOrders, // 等待指令下达
            Executing,     // 正在执行，监控Chore
            Done
        }

        private enum Stage
        {
            Digging,
            Building,
            Plumbing,
            Wiring,
            Automation,
            Complete
        }

        private Blueprint _blueprint;
        private int _anchorCell;
        private DeploymentPhase _currentPhase = DeploymentPhase.NotStarted;
        private Stage _currentStage = Stage.Digging;
        private PrioritySetting _masterPriority;

        private List<Chore> _trackedChores = new List<Chore>();
        private HashSet<int> _cellsInCurrentStage = new HashSet<int>();

        public void Initialize(Blueprint blueprint, int anchorCell)
        {
            this._blueprint = blueprint;
            this._anchorCell = anchorCell;
            this._masterPriority = new PrioritySetting(PriorityScreen.PriorityClass.basic, 5);
            if (BuildTool.Instance != null)
            {
                this._masterPriority = PlanScreen.Instance.GetBuildingPriority();
            }

            BlueprintPlacer.OnChoreCreated += HandleChoreCreated;
            this._currentPhase = DeploymentPhase.PendingOrders;
        }

        private void OnDestroy()
        {
            BlueprintPlacer.OnChoreCreated -= HandleChoreCreated;
        }

        private void HandleChoreCreated(Chore chore, int cell)
        {
            if (_cellsInCurrentStage.Contains(cell))
            {
                _trackedChores.Add(chore);
            }
        }

        private void Update()
        {
            if (_currentStage != Stage.Complete)
            {
                Tick();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Tick()
        {
            if (PlanScreen.Instance == null || ToolMenu.Instance == null)
            {
                logger.Warning("PlanScreen or ToolMenu not available. Aborting.");
                _currentStage = Stage.Complete;
                return;
            }

            if (_currentPhase == DeploymentPhase.PendingOrders)
            {
                // 重置并执行当前阶段的指令下达
                _cellsInCurrentStage.Clear();
                _trackedChores.Clear();

                bool ordersPlaced = ExecuteCurrentStageOrders();

                if (ordersPlaced)
                {
                    _currentPhase = DeploymentPhase.Executing;
                }
                else
                {
                    // 如果当前阶段没有任务，直接进入下一阶段
                    AdvanceToNextStage();
                }
            }
            else if (_currentPhase == DeploymentPhase.Executing)
            {
                // 移除已完成或失效的Chore
                _trackedChores.RemoveAll(chore => chore == null || chore.isComplete);

                // 如果所有Chore都完成了，进入下一阶段
                if (_trackedChores.Count == 0)
                {
                    AdvanceToNextStage();
                }
            }
        }

        private void AdvanceToNextStage()
        {
            _currentStage++;
            _currentPhase = DeploymentPhase.PendingOrders; // 准备为新阶段下达指令
        }

        private bool ExecuteCurrentStageOrders()
        {
            switch (_currentStage)
            {
                case Stage.Digging: return ExecuteDiggingPhase();
                case Stage.Building: return ExecuteBuildingPhase();
                case Stage.Plumbing: return ExecutePlumbingPhase();
                case Stage.Wiring: return ExecuteWiringPhase();
                case Stage.Automation: return ExecuteAutomationPhase();
                default: return false;
            }
        }

        private bool ExecuteDiggingPhase()
        {
            var allItems = new List<BlueprintItem>();
            allItems.AddRange(_blueprint.Buildings);
            allItems.AddRange(_blueprint.Tiles);

            foreach (var item in allItems)
            {
                var def = Assets.GetBuildingDef(item.PrefabID);
                if (def == null) continue;

                int baseCell = Grid.OffsetCell(_anchorCell, new CellOffset(item.Offset.x, item.Offset.y));
                var area = def.PlacementOffsets;
                foreach (var offset in area)
                {
                    int cell = Grid.OffsetCell(baseCell, offset);
                    if (Grid.IsValidCell(cell))
                    {
                        _cellsInCurrentStage.Add(cell);
                    }
                }
            }

            foreach (var cell in _cellsInCurrentStage)
            {
                DigTool.PlaceDig(cell, 0);
            }
            return _cellsInCurrentStage.Count > 0;
        }

        private bool ExecuteBuildingPhase()
        {
            foreach (var item in _blueprint.Buildings)
            {
                var def = Assets.GetBuildingDef(item.PrefabID);
                if (def == null) continue;

                int cell = Grid.OffsetCell(_anchorCell, new CellOffset(item.Offset.x, item.Offset.y));
                if (!Grid.IsValidCell(cell)) continue;

                _cellsInCurrentStage.Add(cell);
                var selectedElements = new List<Tag> { item.Element.CreateTag() };
                def.TryPlace(BuildTool.Instance.visualizer, Grid.CellToPosCBC(cell, def.SceneLayer), item.Orientation, selectedElements, null, true, 0);
            }
            return _blueprint.Buildings.Count > 0;
        }

        private bool ExecutePlumbingPhase() => ProcessConduits(ConduitType.Liquid, ConduitType.Gas);
        private bool ExecuteWiringPhase() => ProcessConduits(ConduitType.Solid);
        private bool ExecuteAutomationPhase() => false;

        private bool ProcessConduits(params ConduitType[] validConduitTypes)
        {
            bool ordersPlaced = false;
            foreach (var item in _blueprint.Tiles)
            {
                var def = Assets.GetBuildingDef(item.PrefabID);
                if (def == null) continue;

                var conduit = def.BuildingComplete.GetComponent<Conduit>();
                if (conduit == null) continue;

                var conduitType = conduit.type;
                bool isValidType = Array.Exists(validConduitTypes, type => type == conduitType);
                if (!isValidType) continue;

                int cell = Grid.OffsetCell(_anchorCell, new CellOffset(item.Offset.x, item.Offset.y));
                if (!Grid.IsValidCell(cell)) continue;

                var element = ElementLoader.GetElement(item.Element.CreateTag());
                if (element == null) continue;

                _cellsInCurrentStage.Add(cell);
                ordersPlaced = true;

                if (def.BuildingComplete != null)
                {
                    var selectedElements = new List<Tag> { item.Element.CreateTag() };
                    def.TryPlace(BuildTool.Instance.visualizer, Grid.CellToPosCBC(cell, def.SceneLayer), item.Orientation, selectedElements, null, true, 0);
                }
                else
                {
                    def.TryPlace(BuildTool.Instance.visualizer, Grid.CellToPosCBC(cell, def.SceneLayer), item.Orientation, new List<Tag> { item.Element.CreateTag() }, null, true, 0);
                }
            }
            return ordersPlaced;
        }
    }
}