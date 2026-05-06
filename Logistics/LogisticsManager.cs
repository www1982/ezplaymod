using System.Collections.Generic;
using System.Linq;
using EZPlay.Core;
using EZPlay.Utils;
using UnityEngine;

using EZPlay.Core.Interfaces;

namespace EZPlay.Logistics
{
    public class LogisticsManager : ILogisticsManager
    {
        private readonly EZPlay.Core.Logger logger = new EZPlay.Core.Logger("LogisticsManager");
        private readonly object _lock = new object();
        private readonly Dictionary<string, LogisticsPolicy> Policies = new Dictionary<string, LogisticsPolicy>();
        private float tickTimer = 0f;
        private const float TICK_INTERVAL = 10f;

        public void RegisterPolicy(LogisticsPolicy policy)
        {
            lock (_lock)
            {
                if (policy == null || string.IsNullOrEmpty(policy.policy_id))
                {
                    logger.Error("Cannot register a null policy or a policy with a null/empty ID.");
                    return;
                }
                Policies[policy.policy_id] = policy;
            }
        }

        public void UnregisterPolicy(string policyId)
        {
            lock (_lock)
            {
                Policies.Remove(policyId);
            }
        }

        public void Tick(float deltaTime)
        {
            tickTimer += deltaTime;
            if (tickTimer < TICK_INTERVAL)
            {
                return;
            }
            tickTimer = 0f;

            lock (_lock)
            {
                foreach (var policy in Policies.Values.ToList())
                {
                    policy.Execute();
                }
            }
        }

    }
}