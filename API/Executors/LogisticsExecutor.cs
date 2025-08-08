using EZPlay.Logistics;
using Newtonsoft.Json;
using System;

namespace EZPlay.API.Executors
{
    public static class LogisticsExecutor
    {
        public static object SetPolicy(string jsonPayload)
        {
            if (string.IsNullOrEmpty(jsonPayload))
            {
                return new { success = false, message = "Payload cannot be null." };
            }

            try
            {
                var policy = JsonConvert.DeserializeObject<LogisticsPolicy>(jsonPayload);
                LogisticsManager.RegisterPolicy(policy);
                return new { success = true, policy_id = policy.policy_id };
            }
            catch (Exception e)
            {
                return new { success = false, message = e.Message };
            }
        }

        private class RemovePolicyPayload
        {
            public string policy_id { get; set; }
        }

        public static object RemovePolicy(string jsonPayload)
        {
            if (string.IsNullOrEmpty(jsonPayload))
            {
                return new { success = false, message = "Payload cannot be null." };
            }

            try
            {
                var data = JsonConvert.DeserializeObject<RemovePolicyPayload>(jsonPayload);
                string policyId = data.policy_id;

                if (string.IsNullOrEmpty(policyId))
                {
                    return new { success = false, message = "policy_id cannot be null or empty." };
                }

                LogisticsManager.UnregisterPolicy(policyId);
                return new { success = true, policy_id = policyId };
            }
            catch (Exception e)
            {
                return new { success = false, message = e.Message };
            }
        }
    }
}