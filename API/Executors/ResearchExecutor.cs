using System;
using System.Collections.Generic;
using System.Linq;
using EZPlay.API.Models;
using EZPlay.API.Exceptions;
using EZPlay.Core;
using EZPlay.Utils;
using Newtonsoft.Json.Linq;
using System.Reflection;
using TUNING;
using UnityEngine;

namespace EZPlay.API.Executors
{
    public static class ResearchExecutor
    {
        private static EZPlay.Core.Interfaces.ILogger Logger => EZPlay.Core.ServiceContainer.Resolve<EZPlay.Core.Interfaces.ILogger>();
        
        public static object HandleResearchAction(string action, int worldId, JObject payload)
        {
            // Research is global, so worldId is ignored for now.
            // This maintains API consistency.
            switch (action)
            {
                case "Research.SetQueue":
                    return SetResearchQueue(payload);
                case "Research.CancelActive":
                    return CancelActiveResearch();
                default:
                    throw new ApiException(400, $"Unknown research action: {action}");
            }
        }

        private static ExecutionResult SetResearchQueue(JObject payload)
        {
            if (payload == null)
            {
                throw new ApiException(400, "Payload cannot be null.");
            }

            var techIdsToken = payload["tech_ids"];
            if (techIdsToken == null || techIdsToken.Type != JTokenType.Array)
            {
                throw new ApiException(400, "Payload must contain a 'tech_ids' array.");
            }
            var techIds = techIdsToken as JArray;

            var research = Research.Instance;
            if (research == null)
            {
                throw new ApiException(500, "Research instance not found.");
            }

            research.SetActiveResearch(null, true); // Clear the queue
            foreach (var techIdToken in techIds)
            {
                var techId = techIdToken.ToString();
                var tech = Db.Get().Techs.Get(techId);
                if (tech != null)
                {
                    if (tech.IsComplete())
                    {
                        Logger.Warning($"Tech '{tech.Name}' is already researched. Skipping.");
                        continue;
                    }
                    research.SetActiveResearch(tech, false);
                }
                else
                {
                    Logger.Warning($"Tech with id '{techId}' not found. Skipping.");
                }
            }

            return new ExecutionResult { Success = true, Message = "Research queue updated." };
        }

        private static ExecutionResult CancelActiveResearch()
        {
            var research = Research.Instance;
            if (research == null)
            {
                throw new ApiException(500, "Research instance not found.");
            }

            var activeResearch = research.GetActiveResearch();
            if (activeResearch == null)
            {
                return new ExecutionResult { Success = true, Message = "No active research to cancel." };
            }

            research.CancelResearch(activeResearch.tech, true);
            return new ExecutionResult { Success = true, Message = $"Cancelled active research: {activeResearch.tech.Name}" };
        }
    }
}
