using System.Collections.Generic;
using System.Linq;
using EZPlay.Core;
using EZPlay.Utils;
using UnityEngine;

namespace EZPlay.Logistics
{
    public static class LogisticsManager
    {
        private static readonly EZPlay.Core.Logger logger = new EZPlay.Core.Logger("LogisticsManager");
        private static readonly Dictionary<string, LogisticsPolicy> Policies = new Dictionary<string, LogisticsPolicy>();
        private static float tickTimer = 0f;
        private const float TICK_INTERVAL = 10f;

        public static void RegisterPolicy(LogisticsPolicy policy)
        {
            if (policy == null || string.IsNullOrEmpty(policy.policy_id))
            {
                logger.Error("Cannot register a null policy or a policy with a null/empty ID.");
                return;
            }
            Policies[policy.policy_id] = policy;
        }

        public static void UnregisterPolicy(string policyId)
        {
            Policies.Remove(policyId);
        }

        public static void Tick(float deltaTime)
        {
            tickTimer += deltaTime;
            if (tickTimer < TICK_INTERVAL)
            {
                return;
            }
            tickTimer = 0f;

            foreach (var policy in Policies.Values.ToList())
            {
                if (policy.policy_type == PolicyType.CONSOLIDATE)
                {
                    ExecuteConsolidatePolicy(policy);
                }
            }
        }

        private static void ExecuteConsolidatePolicy(LogisticsPolicy policy)
        {
            logger.Info($"Executing CONSOLIDATE policy: {policy.policy_id}");
        }
    }
}