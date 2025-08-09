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
        public static ExecutionResult SetPolicy(string jsonPayload)
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

                LogisticsManager.RegisterPolicy(policy);
                return new ExecutionResult { Success = true, Message = $"Policy '{policy.policy_id}' registered.", Data = new { policy_id = policy.policy_id } };
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

        public static ExecutionResult RemovePolicy(string jsonPayload)
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
                return new ExecutionResult { Success = true, Message = $"Policy '{data.policy_id}' unregistered.", Data = new { policy_id = data.policy_id } };
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