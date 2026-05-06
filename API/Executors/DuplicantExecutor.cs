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
    public static class DuplicantExecutor
    {
        private static EZPlay.Core.Interfaces.ILogger Logger => EZPlay.Core.ServiceContainer.Resolve<EZPlay.Core.Interfaces.ILogger>();
        // Priority mapping from string to game enum and value
        private static readonly Dictionary<string, (PriorityScreen.PriorityClass, int)> PriorityMap =
            new Dictionary<string, (PriorityScreen.PriorityClass, int)>(StringComparer.OrdinalIgnoreCase)
            {
                { "Disabled", (PriorityScreen.PriorityClass.basic, 0) },
                { "VeryLow", (PriorityScreen.PriorityClass.basic, 1) },
                { "Low", (PriorityScreen.PriorityClass.basic, 2) },
                { "Standard", (PriorityScreen.PriorityClass.basic, 3) },
                { "High", (PriorityScreen.PriorityClass.high, 4) },
                { "VeryHigh", (PriorityScreen.PriorityClass.topPriority, 5) },
                { "TopPriority", (PriorityScreen.PriorityClass.topPriority, 5) } // Alias for VeryHigh
            };

        public static object HandleDuplicantAction(string action, int worldId, JObject payload)
        {
            switch (action)
            {
                case "Duplicant.SetPriorities":
                    return SetPriorities(worldId, payload);
                case "Duplicant.BatchSetPriorities":
                    return BatchSetPriorities(worldId, payload);
                case "Duplicant.LearnSkill":
                    return LearnSkill(worldId, payload);
                case "Duplicant.SetConsumables":
                    return SetConsumables(worldId, payload);
                case "Duplicant.CanReach":
                    return CanReach(worldId, payload);
                default:
                    throw new ApiException(400, $"Unknown duplicant action: {action}");
            }
        }

        private static ExecutionResult SetPriorities(int worldId, JObject payload)
        {
            if (payload == null)
            {
                throw new ApiException(400, "Payload cannot be null.");
            }

            var duplicantIdToken = payload["duplicant_id"];
            if (duplicantIdToken == null || duplicantIdToken.Type == JTokenType.Null)
            {
                throw new ApiException(400, "Payload must contain a 'duplicant_id' field.");
            }
            var duplicantId = duplicantIdToken.ToString();

            var prioritiesToken = payload["priorities"];
            if (prioritiesToken == null || prioritiesToken.Type != JTokenType.Object)
            {
                throw new ApiException(400, "Payload must contain a 'priorities' object.");
            }
            var priorities = prioritiesToken as JObject;

            return SetDuplicantPriorities(worldId, duplicantId, priorities);
        }

        private static ExecutionResult SetDuplicantPriorities(int worldId, string duplicantId, JObject priorities)
        {
            var result = TrySetDuplicantPriorities(worldId, duplicantId, priorities);
            if (!result.Success)
            {
                // Re-throw as an exception to maintain original behavior for single-set API
                throw new ApiException(result.StatusCode, result.Message, result.Data);
            }
            return result;
        }

        private static ExecutionResult TrySetDuplicantPriorities(int worldId, string duplicantId, JObject priorities)
        {
            var minionIdentity = Components.LiveMinionIdentities.GetWorldItems(worldId).FirstOrDefault(m => m.name == duplicantId || m.GetProperName() == duplicantId);
            if (minionIdentity == null)
            {
                return new ExecutionResult { Success = false, StatusCode = 404, Message = $"Duplicant with id '{duplicantId}' not found in world {worldId}.", Data = new { duplicant_id = duplicantId, world_id = worldId } };
            }

            var choreConsumer = minionIdentity.GetComponent<ChoreConsumer>();
            if (choreConsumer == null)
            {
                return new ExecutionResult { Success = false, StatusCode = 500, Message = "ChoreConsumer component not found on duplicant.", Data = new { duplicant_id = duplicantId } };
            }

            var appliedPriorities = new Dictionary<string, string>();
            var failedPriorities = new Dictionary<string, string>();

            foreach (var property in priorities.Properties())
            {
                var choreGroupName = property.Name;
                var priorityString = property.Value.ToString();

                var choreGroup = Db.Get().ChoreGroups.resources.FirstOrDefault(group => group.Id == choreGroupName);
                if (choreGroup == null)
                {
                    Logger.Warning($"Chore group '{choreGroupName}' not found. Skipping.");
                    failedPriorities[choreGroupName] = "Chore group not found.";
                    continue;
                }

                if (PriorityMap.TryGetValue(priorityString, out var priorityValue))
                {
                    choreConsumer.SetPersonalPriority(choreGroup, priorityValue.Item2);
                    appliedPriorities[choreGroupName] = priorityString;
                }
                else
                {
                    Logger.Warning($"Unknown priority value '{priorityString}' for chore group '{choreGroupName}'. Skipping.");
                    failedPriorities[choreGroupName] = $"Unknown priority value '{priorityString}'.";
                }
            }

            return new ExecutionResult
            {
                Success = true,
                Message = $"Priorities set for duplicant '{duplicantId}'.",
                Data = new { duplicant_id = duplicantId, applied = appliedPriorities, failed = failedPriorities }
            };
        }

        private static ExecutionResult BatchSetPriorities(int worldId, JObject payload)
        {
            if (payload?["requests"] is not JArray requests)
            {
                throw new ApiException(400, "Payload must contain a 'requests' array.");
            }

            var results = new List<ExecutionResult>();
            foreach (var requestToken in requests)
            {
                if (requestToken is not JObject request)
                {
                    results.Add(new ExecutionResult { Success = false, StatusCode = 400, Message = "Invalid request item: not a JSON object." });
                    continue;
                }

                var duplicantIdToken = request["duplicant_id"];
                if (duplicantIdToken == null || duplicantIdToken.Type == JTokenType.Null)
                {
                    results.Add(new ExecutionResult { Success = false, StatusCode = 400, Message = "Request must contain a 'duplicant_id' field." });
                    continue;
                }
                var duplicantId = duplicantIdToken.ToString();

                if (request["priorities"] is not JObject priorities)
                {
                    results.Add(new ExecutionResult { Success = false, StatusCode = 400, Message = "Request must contain a 'priorities' object.", Data = new { duplicant_id = duplicantId } });
                    continue;
                }

                var result = TrySetDuplicantPriorities(worldId, duplicantId, priorities);
                results.Add(result);
            }

            var hasFailures = results.Any(r => !r.Success);
            var hasSuccesses = results.Any(r => r.Success);

            var overallStatus = "partial_success";
            if (!hasFailures) overallStatus = "success";
            if (!hasSuccesses) overallStatus = "failure";

            return new ExecutionResult
            {
                Success = !hasFailures,
                Message = $"Batch priority update completed with status: {overallStatus}.",
                Data = new { status = overallStatus, results = results }
            };
        }

        private static ExecutionResult LearnSkill(int worldId, JObject payload)
        {
            if (payload == null)
            {
                throw new ApiException(400, "Payload cannot be null.");
            }

            var duplicantIdToken = payload["duplicant_id"];
            if (duplicantIdToken == null || duplicantIdToken.Type == JTokenType.Null)
            {
                throw new ApiException(400, "Payload must contain a 'duplicant_id' field.");
            }
            var duplicantId = duplicantIdToken.ToString();

            var skillIdToken = payload["skill_id"];
            if (skillIdToken == null || skillIdToken.Type == JTokenType.Null)
            {
                throw new ApiException(400, "Payload must contain a 'skill_id' field.");
            }
            var skillId = skillIdToken.ToString();

            var minionIdentity = Components.LiveMinionIdentities.GetWorldItems(worldId).FirstOrDefault(m => m.name == duplicantId || m.GetProperName() == duplicantId);
            if (minionIdentity == null)
            {
                throw new ApiException(404, $"Duplicant with id '{duplicantId}' not found in world {worldId}.");
            }

            var minionResume = minionIdentity.GetComponent<MinionResume>();
            if (minionResume == null)
            {
                throw new ApiException(500, "MinionResume component not found on duplicant.");
            }

            if (Db.Get().Skills.Get(skillId) == null)
            {
                throw new ApiException(404, $"Skill with id '{skillId}' not found.");
            }

            if (!minionResume.CanMasterSkill(minionResume.GetSkillMasteryConditions(skillId)))
            {
                throw new ApiException(400, "Cannot master skill: prerequisites not met or not enough skill points.");
            }

            minionResume.MasterSkill(skillId);

            return new ExecutionResult { Success = true, Message = $"Skill '{skillId}' learned by duplicant '{duplicantId}'." };
        }

        private static ExecutionResult SetConsumables(int worldId, JObject payload)
        {
            if (payload == null)
            {
                throw new ApiException(400, "Payload cannot be null.");
            }

            var duplicantIdToken = payload["duplicant_id"];
            if (duplicantIdToken == null || duplicantIdToken.Type == JTokenType.Null)
            {
                throw new ApiException(400, "Payload must contain a 'duplicant_id' field.");
            }
            var duplicantId = duplicantIdToken.ToString();

            var minionIdentity = Components.LiveMinionIdentities.GetWorldItems(worldId).FirstOrDefault(m => m.name == duplicantId || m.GetProperName() == duplicantId);
            if (minionIdentity == null)
            {
                throw new ApiException(404, $"Duplicant with id '{duplicantId}' not found in world {worldId}.");
            }

            var consumer = minionIdentity.GetComponent<ConsumableConsumer>();
            if (consumer == null)
            {
                throw new ApiException(500, "ConsumableConsumer component not found on duplicant.");
            }

            if (payload["allowed"] is JArray allowed)
            {
                foreach (var consumableId in allowed.Select(t => t.ToString()))
                {
                    consumer.SetPermitted(consumableId, true);
                }
            }

            if (payload["disallowed"] is JArray disallowed)
            {
                foreach (var consumableId in disallowed.Select(t => t.ToString()))
                {
                    consumer.SetPermitted(consumableId, false);
                }
            }

            return new ExecutionResult { Success = true, Message = $"Consumable permissions updated for duplicant '{duplicantId}'." };
        }

        private static ExecutionResult CanReach(int worldId, JObject payload)
        {
            if (payload == null)
            {
                throw new ApiException(400, "Payload cannot be null.");
            }

            var duplicantIdToken = payload["duplicant_id"];
            if (duplicantIdToken == null || duplicantIdToken.Type == JTokenType.Null)
            {
                throw new ApiException(400, "Payload must contain a 'duplicant_id' field.");
            }
            var duplicantId = duplicantIdToken.ToString();

            var xToken = payload["target_x"];
            var yToken = payload["target_y"];
            if (xToken == null || yToken == null)
            {
                throw new ApiException(400, "Payload must contain 'target_x' and 'target_y' fields.");
            }

            int x = xToken.Value<int>();
            int y = yToken.Value<int>();
            int cell = Grid.PosToCell(new Vector3(x, y, 0));

            var minionIdentity = Components.LiveMinionIdentities.GetWorldItems(worldId).FirstOrDefault(m => m.name == duplicantId || m.GetProperName() == duplicantId);
            if (minionIdentity == null)
            {
                throw new ApiException(404, $"Duplicant with id '{duplicantId}' not found in world {worldId}.");
            }

            var navigator = minionIdentity.GetComponent<Navigator>();
            if (navigator == null)
            {
                throw new ApiException(500, "Navigator component not found on duplicant.");
            }

            bool canReach = navigator.CanReach(cell);
            
            return new ExecutionResult 
            { 
                Success = true, 
                Message = $"Reachability query completed.",
                Data = new { canReach = canReach }
            };
        }
    }
}
