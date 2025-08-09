using System;
using EZPlay.Blueprints;
using EZPlay.API.Models;
using EZPlay.API.Exceptions;
using Newtonsoft.Json;
using UnityEngine;

namespace EZPlay.API.Executors
{
    /// <summary>
    /// Handles API requests related to blueprint creation and management.
    /// </summary>
    public static class BlueprintExecutor
    {
        private static readonly EZPlay.Core.Logger logger = new EZPlay.Core.Logger("BlueprintExecutor");

        /// <summary>
        /// Scans a specified area in the game world and creates a blueprint from it.
        /// </summary>
        /// <param name="jsonPayload">A JSON string containing the 'name' for the blueprint and the 'area' to scan.</param>
        /// <returns>An object containing the result of the operation, either the created blueprint or an error message.</returns>
        public static ExecutionResult CreateFromGame(string jsonPayload)
        {
            if (string.IsNullOrEmpty(jsonPayload))
            {
                throw new ApiException(400, "Payload cannot be null or empty.");
            }

            CreateFromGamePayload payload;
            try
            {
                payload = JsonConvert.DeserializeObject<CreateFromGamePayload>(jsonPayload);
            }
            catch (JsonException ex)
            {
                throw new ApiException(400, $"Invalid JSON format: {ex.Message}");
            }

            if (payload == null)
            {
                throw new ApiException(400, "Invalid payload structure. Could not deserialize.");
            }

            if (string.IsNullOrEmpty(payload.Name))
            {
                throw new ApiException(400, "Payload must contain a 'name' field.");
            }

            if (payload.Area.width <= 0 || payload.Area.height <= 0)
            {
                throw new ApiException(400, "Payload 'area' must have a positive width and height.");
            }

            try
            {
                var scannedBlueprint = BlueprintScanner.ScanAreaToBlueprint(payload.Name, payload.Area);
                return new ExecutionResult { Success = true, Message = "Blueprint created successfully.", Data = scannedBlueprint };
            }
            catch (Exception ex)
            {
                // Catching potential exceptions from BlueprintScanner for robustness
                throw new ApiException(500, $"An unexpected error occurred during blueprint scanning: {ex.Message}");
            }
        }

        /// <summary>
        /// Defines the expected payload structure for the CreateFromGame request.
        /// </summary>
        private class CreateFromGamePayload
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("area")]
            public Rect Area { get; set; }
        }
    }
}