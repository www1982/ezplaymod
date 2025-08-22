using System;
using System.Collections.Generic;
using UnityEngine;
using EZPlay.Core;
using Klei.AI;

namespace EZPlay.Blueprints
{
    public static class BlueprintPlacer
    {
        public static void PlaceBlueprint(int worldId, Blueprint blueprint, int anchorCell)
        {
            var placerGo = new GameObject("BlueprintPlacerInstance");
            var placer = placerGo.AddComponent<BlueprintPlacerInstance>();
            placer.Initialize(worldId, blueprint, anchorCell);
        }
    }

    public class BlueprintPlacerInstance : MonoBehaviour
    {
        private static readonly EZPlay.Core.Logger logger = EZPlay.Core.ServiceContainer.Resolve<EZPlay.Core.Logger>();

        private enum DeploymentPhase
        {
            NotStarted,
            PendingOrders,
            Executing,
            Done
        }

        private enum Stage
        {
            Digging,
            Building,
            Plumbing,
            Wiring,
            Automation,
            Complete,
            Error
        }

        private Blueprint _blueprint;
        private int _anchorCell;
        private int _worldId;
        private DeploymentPhase _currentPhase = DeploymentPhase.NotStarted;
        private Stage _currentStage = Stage.Digging;
        private PrioritySetting _masterPriority;

        private HashSet<int> _cellsInCurrentStage = new HashSet<int>();
        private List<(int cell, string prefabID, ObjectLayer layer)> _expectedPlacements = new List<(int, string, ObjectLayer)>();

        private float _startTime;
        private const float StageTimeout = 60f;
        private Guid _instanceId = Guid.NewGuid();

        public void Initialize(int worldId, Blueprint blueprint, int anchorCell)
        {
            this._worldId = worldId;
            this._blueprint = blueprint;
            this._anchorCell = anchorCell;
            this._masterPriority = new PrioritySetting(PriorityScreen.PriorityClass.basic, 5);
            if (BuildTool.Instance != null)
            {
                this._masterPriority = PlanScreen.Instance.GetBuildingPriority();
            }

            this._currentPhase = DeploymentPhase.PendingOrders;
        }

        private void OnDestroy()
        {
            if (_currentStage != Stage.Complete && _currentStage != Stage.Error)
            {
                Rollback();
            }
        }

        private void Update()
        {
            if (_currentStage != Stage.Complete && _currentStage != Stage.Error)
            {
                Tick();
            }
            else
            {
                if (_currentStage == Stage.Error)
                {
                    Rollback();
                    _currentStage = Stage.Complete;
                }
                Destroy(gameObject);
            }
        }

        private void Tick()
        {
            if (PlanScreen.Instance == null || ToolMenu.Instance == null)
            {
                logger.Warning("PlanScreen or ToolMenu not available. Aborting.");
                _currentStage = Stage.Error;
                return;
            }

            if (_currentPhase == DeploymentPhase.PendingOrders)
            {
                _cellsInCurrentStage.Clear();
                _expectedPlacements.Clear();

                bool ordersPlaced = ExecuteCurrentStageOrders();

                if (ordersPlaced)
                {
                    _currentPhase = DeploymentPhase.Executing;
                    _startTime = Time.time;
                }
                else
                {
                    AdvanceToNextStage();
                }
            }
            else if (_currentPhase == DeploymentPhase.Executing)
            {
                if (IsStageComplete())
                {
                    AdvanceToNextStage();
                }
                else if (Time.time - _startTime > StageTimeout)
                {
                    logger.Warning($"Stage {_currentStage} timed out for instance {_instanceId}");
                    _currentStage = Stage.Error;
                }
            }
        }

        private void AdvanceToNextStage()
        {
            if (_currentStage == Stage.Automation)
            {
                _currentStage = Stage.Complete;
            }
            else
            {
                _currentStage++;
            }
            _currentPhase = DeploymentPhase.PendingOrders;
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

        private bool IsStageComplete()
        {
            switch (_currentStage)
            {
                case Stage.Digging:
                    foreach (var cell in _cellsInCurrentStage)
                    {
                        if (Grid.Solid[cell])
                            return false;
                    }
                    return true;
                default:
                    foreach (var placement in _expectedPlacements)
                    {
                        var obj = Grid.Objects[placement.cell, (int)placement.layer];
                        if (obj == null)
                            return false;
                        var building = obj.GetComponent<BuildingComplete>();
                        if (building == null || building.Def.PrefabID != placement.prefabID)
                            return false;
                    }
                    return true;
            }
        }

        private void Rollback()
        {
            logger.Info($"Rolling back blueprint placement for instance {_instanceId}");

            // Cancel pending chores
            // TODO: 修复访问Chores的方式，目前注释以通过编译
            // foreach (Chore chore in global::Components.Chores.Items)
            // {
            //     if (chore == null || chore.target == null || chore.target.gameObject == null) continue;

            //     int targetCell = Grid.PosToCell(chore.target.gameObject);

            //     if (_cellsInCurrentStage.Contains(targetCell))
            //     {
            //         chore.Cancel("Blueprint rollback");
            //     }
            // }

            // Deconstruct partial builds
            foreach (var placement in _expectedPlacements)
            {
                var obj = Grid.Objects[placement.cell, (int)placement.layer];
                if (obj != null)
                {
                    var deconstructable = obj.GetComponent<Deconstructable>();
                    if (deconstructable != null)
                    {
                        deconstructable.QueueDeconstruction(true);
                    }
                }
            }

            _cellsInCurrentStage.Clear();
            _expectedPlacements.Clear();
        }

        private bool ExecuteDiggingPhase()
        {
            var allItems = new List<BlueprintItem>();
            allItems.AddRange(_blueprint.Buildings);
            allItems.AddRange(_blueprint.Tiles);

            bool placed = false;
            foreach (var item in allItems)
            {
                var def = Assets.GetBuildingDef(item.PrefabID);
                if (def == null) continue;

                int baseCell = Grid.OffsetCell(_anchorCell, new CellOffset(item.Offset.x, item.Offset.y));
                var area = def.PlacementOffsets ?? new CellOffset[] { CellOffset.none };

                foreach (var offset in area)
                {
                    int cell = Grid.OffsetCell(baseCell, offset);
                    if (Grid.IsValidCell(cell) && Grid.Solid[cell])
                    {
                        _cellsInCurrentStage.Add(cell);
                        DigTool.PlaceDig(cell, 0);
                        placed = true;
                    }
                }
            }
            return placed;
        }

        private bool ExecuteBuildingPhase()
        {
            bool placed = false;
            foreach (var item in _blueprint.Buildings)
            {
                var def = Assets.GetBuildingDef(item.PrefabID);
                if (def == null) continue;

                int cell = Grid.OffsetCell(_anchorCell, new CellOffset(item.Offset.x, item.Offset.y));
                if (!Grid.IsValidCell(cell)) continue;

                _cellsInCurrentStage.Add(cell);

                var selectedElements = new List<Tag> { item.Element.CreateTag() };
                def.TryPlace(null, Grid.CellToPosCBC(cell, def.SceneLayer), item.Orientation, selectedElements);

                _expectedPlacements.Add((cell, item.PrefabID, def.ObjectLayer));
                placed = true;
            }
            return placed;
        }

        private bool ExecutePlumbingPhase() => ProcessConduits(ConduitType.Liquid, ConduitType.Gas);
        private bool ExecuteWiringPhase() => ProcessConduits(ConduitType.Solid);
        private bool ExecuteAutomationPhase() => false;

        private bool ProcessConduits(params ConduitType[] validConduitTypes)
        {
            bool placed = false;
            foreach (var item in _blueprint.Tiles)
            {
                var def = Assets.GetBuildingDef(item.PrefabID);
                if (def == null) continue;

                var conduit = def.BuildingComplete.GetComponent<Conduit>();
                if (conduit == null) continue;

                var conduitType = conduit.type;
                bool isValidType = false;
                foreach (var validType in validConduitTypes)
                {
                    if (validType == conduitType)
                    {
                        isValidType = true;
                        break;
                    }
                }
                if (!isValidType) continue;

                int cell = Grid.OffsetCell(_anchorCell, new CellOffset(item.Offset.x, item.Offset.y));
                if (!Grid.IsValidCell(cell)) continue;

                var element = ElementLoader.GetElement(item.Element.CreateTag());
                if (element == null) continue;

                _cellsInCurrentStage.Add(cell);

                var selectedElements = new List<Tag> { item.Element.CreateTag() };
                def.TryPlace(null, Grid.CellToPosCBC(cell, def.SceneLayer), item.Orientation, selectedElements);

                ObjectLayer layer = GetLayerForConduitType(conduitType);
                _expectedPlacements.Add((cell, item.PrefabID, layer));

                placed = true;
            }
            return placed;
        }

        private ObjectLayer GetLayerForConduitType(ConduitType type)
        {
            switch (type)
            {
                case ConduitType.Gas:
                    return ObjectLayer.GasConduit;
                case ConduitType.Liquid:
                    return ObjectLayer.LiquidConduit;
                case ConduitType.Solid:
                    return ObjectLayer.SolidConduit;
                default:
                    return ObjectLayer.Building;
            }
        }
    }
}