using System;
using EZPlay.API.Models;
using EZPlay.API.Exceptions;
using Newtonsoft.Json;
using UnityEngine;

namespace EZPlay.API.Executors
{
    public static class BuildingDestroyer
    {
        private static readonly EZPlay.Core.Logger logger = new EZPlay.Core.Logger("BuildingDestroyer");
        private class DestroyRequest
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        /// <summary>
        /// Destroys a building at a specified location in a given world.
        /// </summary>
        /// <param name="worldId">The ID of the world where the building is located.</param>
        /// <param name="jsonPayload">A JSON string containing the 'X' and 'Y' coordinates of the building.</param>
        /// <returns>An execution result indicating success or failure.</returns>
        public static ExecutionResult Execute(int worldId, string jsonPayload)
        {
            if (string.IsNullOrEmpty(jsonPayload))
            {
                throw new ApiException(400, "Payload cannot be null or empty.");
            }

            DestroyRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<DestroyRequest>(jsonPayload);
            }
            catch (JsonException ex)
            {
                throw new ApiException(400, $"Invalid JSON format: {ex.Message}");
            }

            if (request == null)
            {
                throw new ApiException(400, "Invalid payload structure. Requires 'X' and 'Y' integer fields.");
            }

            var worldContainer = ClusterManager.Instance.GetWorld(worldId);
            if (worldContainer == null)
            {
                throw new ApiException(404, $"World with ID {worldId} not found.");
            }

            var cell = Grid.PosToCell(new Vector3(request.X, request.Y, 0));
            if (!Grid.IsValidCellInWorld(cell, worldId))
            {
                throw new ApiException(400, $"Invalid coordinates ({request.X}, {request.Y}) in world {worldId}.");
            }

            var building = Grid.Objects[cell, (int)ObjectLayer.Building];

            if (building != null && building.GetMyWorldId() == worldId)
            {
                Util.KDestroyGameObject(building);
                return new ExecutionResult { Success = true, Message = $"Building at ({request.X}, {request.Y}) in world {worldId} destroyed." };
            }

            throw new ApiException(404, $"No building found at ({request.X}, {request.Y}) in world {worldId}.");
        }
    }
}