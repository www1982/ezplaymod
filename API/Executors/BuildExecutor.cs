using System;
using System.Collections.Generic;
using System.Linq;
using EZPlay.API.Models;
using EZPlay.API.Exceptions;
using Newtonsoft.Json;
using UnityEngine;

namespace EZPlay.API.Executors
{
    public static class BuildExecutor
    {
        private class BuildRequest
        {
            public int X { get; set; }
            public int Y { get; set; }
            public string BuildingId { get; set; }
            public string[] Materials { get; set; }
            public int Orientation { get; set; } = 0;
            public int Priority { get; set; } = 5;
        }

        public static ExecutionResult Execute(int worldId, string jsonPayload)
        {
            if (string.IsNullOrEmpty(jsonPayload))
                throw new ApiException(400, "Payload cannot be null or empty.");

            BuildRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<BuildRequest>(jsonPayload);
            }
            catch (JsonException ex)
            {
                throw new ApiException(400, $"Invalid JSON format: {ex.Message}");
            }

            if (string.IsNullOrEmpty(request.BuildingId))
                throw new ApiException(400, "BuildingId is required.");
            if (request.Materials == null || request.Materials.Length == 0)
                throw new ApiException(400, "Materials array is required and cannot be empty.");

            var cell = Grid.PosToCell(new Vector3(request.X, request.Y, 0));
            
            if (!Grid.IsValidCellInWorld(cell, worldId))
            {
                throw new ApiException(400, $"Invalid coordinates ({request.X}, {request.Y}) in world {worldId}.");
            }

            BuildingDef def = Assets.GetBuildingDef(request.BuildingId);
            if (def == null)
            {
                throw new ApiException(404, $"Building definition '{request.BuildingId}' not found.");
            }

            IList<Tag> elements = request.Materials.Select(m => new Tag(m)).ToList();
            Orientation orient = (Orientation)request.Orientation;

            // Create the construction site (ghost)
            GameObject site = def.TryPlace(null, Grid.CellToPosCBC(cell, def.SceneLayer), orient, elements, 0);
            
            if (site == null)
            {
                throw new ApiException(400, $"Failed to place building construction site at ({request.X}, {request.Y}). Location may be blocked or invalid.");
            }

            Prioritizable prioritizable = site.GetComponent<Prioritizable>();
            if (prioritizable != null)
            {
                int pVal = Mathf.Clamp(request.Priority, 1, 9);
                prioritizable.SetMasterPriority(new PrioritySetting(PriorityScreen.PriorityClass.basic, pVal));
            }

            return new ExecutionResult { Success = true, Message = $"Construction site for {request.BuildingId} placed at ({request.X}, {request.Y}) with priority {request.Priority}." };
        }
    }
}
