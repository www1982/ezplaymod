using System;
using EZPlay.API.Models;
using EZPlay.API.Exceptions;
using Newtonsoft.Json;
using UnityEngine;

namespace EZPlay.API.Executors
{
    public static class DigExecutor
    {
        private class DigRequest
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Priority { get; set; } = 5;
        }

        public static ExecutionResult Execute(int worldId, string jsonPayload)
        {
            if (string.IsNullOrEmpty(jsonPayload))
                throw new ApiException(400, "Payload cannot be null or empty.");

            DigRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<DigRequest>(jsonPayload);
            }
            catch (JsonException ex)
            {
                throw new ApiException(400, $"Invalid JSON format: {ex.Message}");
            }

            var cell = Grid.PosToCell(new Vector3(request.X, request.Y, 0));
            
            if (!Grid.IsValidCellInWorld(cell, worldId))
            {
                throw new ApiException(400, $"Invalid coordinates ({request.X}, {request.Y}) in world {worldId}.");
            }

            if (!Grid.Solid[cell])
            {
                return new ExecutionResult { Success = true, Message = "Cell is already empty (not solid)." };
            }

            // Removed ObjectLayer.Digs check to avoid compilation error

            var prefab = Assets.GetPrefab(new Tag("DigPlacer"));
            if (prefab == null)
            {
                throw new ApiException(500, "DigPlacer prefab not found.");
            }

            GameObject digPlacer = GameUtil.KInstantiate(prefab, Grid.CellToPosCBC(cell, Grid.SceneLayer.Move), Grid.SceneLayer.Move, null, 0);
            Grid.Objects[cell, 7] = digPlacer; // 7 is ObjectLayer.Digs
            digPlacer.SetActive(true);

            Prioritizable prioritizable = digPlacer.GetComponent<Prioritizable>();
            if (prioritizable != null)
            {
                int pVal = Mathf.Clamp(request.Priority, 1, 9);
                prioritizable.SetMasterPriority(new PrioritySetting(PriorityScreen.PriorityClass.basic, pVal));
            }

            return new ExecutionResult { Success = true, Message = $"Dig command issued at ({request.X}, {request.Y}) with priority {request.Priority}." };
        }
    }
}
