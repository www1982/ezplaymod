using System;
using EZPlay.Blueprints;
using Newtonsoft.Json;
using UnityEngine;

namespace EZPlay.API.Executors
{
    /// <summary>
    /// Handles API requests related to blueprint creation and management.
    /// </summary>
    public static class BlueprintExecutor
    {
        /// <summary>
        /// Scans a specified area in the game world and creates a blueprint from it.
        /// </summary>
        /// <param name="jsonPayload">A JSON string containing the 'name' for the blueprint and the 'area' to scan.</param>
        /// <returns>An object containing the result of the operation, either the created blueprint or an error message.</returns>
        public static object CreateFromGame(string jsonPayload)
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<CreateFromGamePayload>(jsonPayload);
                if (payload == null)
                {
                    throw new ArgumentException("Invalid payload structure.");
                }

                var scannedBlueprint = BlueprintScanner.ScanAreaToBlueprint(payload.Name, payload.Area);

                return new { success = true, blueprint = scannedBlueprint };
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
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