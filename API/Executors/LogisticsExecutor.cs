using EZPlay.Logistics;
using EZPlay.API.Models;
using EZPlay.API.Exceptions;
using Newtonsoft.Json;
using System;

namespace EZPlay.API.Executors
{
    public static class LogisticsExecutor
    {
        private static readonly EZPlay.Core.Logger logger = new EZPlay.Core.Logger("LogisticsExecutor");
        /// <summary>
        /// Sets a logistics policy. This is currently a global setting.
        /// </summary>
        /// <param name="worldId">The ID of the world (currently ignored, but included for API consistency).</param>
        /// <param name="jsonPayload">A JSON string representing the logistics policy.</param>
        /// <returns>An execution result indicating success or failure.</returns>
        public static ExecutionResult SetPolicy(int worldId, string jsonPayload)
        {
            if (string.IsNullOrEmpty(jsonPayload))
            {
                throw new ApiException(400, "Payload cannot be null or empty.");
            }

            try
            {
                var policy = JsonConvert.DeserializeObject<LogisticsPolicy>(jsonPayload);
                if (policy == null || string.IsNullOrEmpty(policy.policy_id))
                {
                    throw new ApiException(400, "Invalid policy format. 'policy_id' is required.");
                }

                // This is a global manager, but we pass worldId for consistency.
                // In the future, policies could become world-specific.
                LogisticsManager.RegisterPolicy(policy);
                return new ExecutionResult { Success = true, Message = $"Policy '{policy.policy_id}' registered.", Data = new { policy_id = policy.policy_id, worldId = worldId } };
            }
            catch (JsonException ex)
            {
                throw new ApiException(400, $"Invalid JSON format: {ex.Message}");
            }
            catch (Exception e)
            {
                throw new ApiException(500, $"An unexpected error occurred: {e.Message}");
            }
        }

        private class RemovePolicyPayload
        {
            public string policy_id { get; set; }
        }

        /// <summary>
        /// Removes a logistics policy. This is currently a global setting.
        /// </summary>
        /// <param name="worldId">The ID of the world (currently ignored, but included for API consistency).</param>
        /// <param name="jsonPayload">A JSON string containing the 'policy_id' to remove.</param>
        /// <returns>An execution result indicating success or failure.</returns>
        public static ExecutionResult RemovePolicy(int worldId, string jsonPayload)
        {
            if (string.IsNullOrEmpty(jsonPayload))
            {
                throw new ApiException(400, "Payload cannot be null or empty.");
            }

            try
            {
                var data = JsonConvert.DeserializeObject<RemovePolicyPayload>(jsonPayload);
                if (data == null || string.IsNullOrEmpty(data.policy_id))
                {
                    throw new ApiException(400, "Invalid payload. 'policy_id' is required.");
                }

                LogisticsManager.UnregisterPolicy(data.policy_id);
                return new ExecutionResult { Success = true, Message = $"Policy '{data.policy_id}' unregistered.", Data = new { policy_id = data.policy_id, worldId = worldId } };
            }
            catch (JsonException ex)
            {
                throw new ApiException(400, $"Invalid JSON format: {ex.Message}");
            }
            catch (Exception e)
            {
                throw new ApiException(500, $"An unexpected error occurred: {e.Message}");
            }
        }
    }
}