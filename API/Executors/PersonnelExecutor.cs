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
    public static class PersonnelExecutor
    {
        private static readonly EZPlay.Core.Logger Logger = EZPlay.Core.ServiceContainer.Resolve<EZPlay.Core.Logger>();
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

        public static object HandleScheduleAction(string action, int worldId, JObject payload)
        {
            switch (action)
            {
                case "Schedule.Create":
                    return CreateSchedule(payload); // Schedules are global, no worldId needed
                case "Schedule.UpdateBlocks":
                    return UpdateScheduleBlocks(payload); // Schedules are global, no worldId needed
                case "Schedule.AssignDuplicant":
                    return AssignDuplicantToSchedule(worldId, payload);
                default:
                    throw new ApiException(400, $"Unknown schedule action: {action}");
            }
        }

        private static ExecutionResult CreateSchedule(JObject payload)
        {
            if (payload == null)
            {
                throw new ApiException(400, "Payload cannot be null.");
            }

            var scheduleNameToken = payload["name"];
            if (scheduleNameToken == null || scheduleNameToken.Type == JTokenType.Null || string.IsNullOrEmpty(scheduleNameToken.ToString()))
            {
                throw new ApiException(400, "Payload must contain a non-empty 'name' field.");
            }
            var scheduleName = scheduleNameToken.ToString();

            var scheduleManager = ScheduleManager.Instance;
            if (scheduleManager == null)
            {
                throw new ApiException(500, "ScheduleManager instance not found.");
            }

            scheduleManager.AddSchedule(Db.Get().ScheduleGroups.allGroups, scheduleName, true);
            return new ExecutionResult { Success = true, Message = $"Schedule '{scheduleName}' created." };
        }

        private static ExecutionResult UpdateScheduleBlocks(JObject payload)
        {
            if (payload == null)
            {
                throw new ApiException(400, "Payload cannot be null.");
            }

            var scheduleNameToken = payload["name"];
            if (scheduleNameToken == null || scheduleNameToken.Type == JTokenType.Null || string.IsNullOrEmpty(scheduleNameToken.ToString()))
            {
                throw new ApiException(400, "Payload must contain a non-empty 'name' field.");
            }
            var scheduleName = scheduleNameToken.ToString();

            var blocksToken = payload["blocks"];
            if (blocksToken == null || blocksToken.Type != JTokenType.Array)
            {
                throw new ApiException(400, "Payload must contain a 'blocks' array.");
            }
            var blocks = blocksToken as JArray;

            var scheduleManager = ScheduleManager.Instance;
            if (scheduleManager == null)
            {
                throw new ApiException(500, "ScheduleManager instance not found.");
            }

            var schedule = scheduleManager.GetSchedules().FirstOrDefault(s => s.name == scheduleName);
            if (schedule == null)
            {
                throw new ApiException(404, $"Schedule '{scheduleName}' not found.");
            }

            var newBlocks = new List<ScheduleBlock>();
            foreach (var blockToken in blocks)
            {
                if (!(blockToken is JObject blockObj)) continue;

                var blockName = blockObj["name"]?.ToString();
                var blockType = blockObj["type_id"]?.ToString();

                if (string.IsNullOrEmpty(blockName) || string.IsNullOrEmpty(blockType)) continue;

                var scheduleGroup = Db.Get().ScheduleGroups.Get(blockType);
                if (scheduleGroup == null)
                {
                    Logger.Warning($"Schedule block type '{blockType}' not found. Skipping.");
                    continue;
                }

                newBlocks.Add(new ScheduleBlock(blockName, scheduleGroup.Id));
            }

            schedule.GetBlocks().Clear();
            schedule.GetBlocks().AddRange(newBlocks);
            return new ExecutionResult { Success = true, Message = $"Schedule '{scheduleName}' blocks updated." };
        }

        private static ExecutionResult AssignDuplicantToSchedule(int worldId, JObject payload)
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

            var scheduleNameToken = payload["schedule_name"];
            if (scheduleNameToken == null || scheduleNameToken.Type == JTokenType.Null)
            {
                throw new ApiException(400, "Payload must contain a 'schedule_name' field.");
            }
            var scheduleName = scheduleNameToken.ToString();

            var minionIdentity = Components.LiveMinionIdentities.GetWorldItems(worldId).FirstOrDefault(m => m.name == duplicantId || m.GetProperName() == duplicantId);
            if (minionIdentity == null)
            {
                throw new ApiException(404, $"Duplicant with id '{duplicantId}' not found in world {worldId}.");
            }

            var scheduleManager = ScheduleManager.Instance;
            if (scheduleManager == null)
            {
                throw new ApiException(500, "ScheduleManager instance not found.");
            }

            var schedule = scheduleManager.GetSchedules().FirstOrDefault(s => s.name == scheduleName);
            if (schedule == null)
            {
                throw new ApiException(404, $"Schedule '{scheduleName}' not found.");
            }

            var minionResume = minionIdentity.GetComponent<MinionResume>();
            if (minionResume == null)
            {
                throw new ApiException(500, "MinionResume component not found on duplicant.");
            }

            var schedulable = minionResume.GetComponent<Schedulable>();
            if (schedulable == null)
            {
                throw new ApiException(500, "Schedulable component not found on duplicant.");
            }

            schedule.Assign(schedulable);
            return new ExecutionResult { Success = true, Message = $"Duplicant '{duplicantId}' assigned to schedule '{scheduleName}'." };
        }

        public static object HandlePrintingPodAction(int worldId, JObject payload)
        {
            var action = payload["action"]?.ToString();
            switch (action)
            {
                case "PrintingPod.GetChoices":
                    return GetPrintingPodChoices(worldId);
                case "PrintingPod.SelectChoice":
                    return SelectPrintingPodChoice(worldId, payload);
                default:
                    throw new ApiException(400, $"Unknown printing pod action: {action}");
            }
        }

        private static ExecutionResult GetPrintingPodChoices(int worldId)
        {
            var telepad = Components.Telepads.GetWorldItems(worldId).FirstOrDefault();
            if (telepad == null)
            {
                throw new ApiException(500, $"Telepad instance not found in world {worldId}.");
            }

            var immigration = Immigration.Instance;
            if (immigration == null)
            {
                throw new ApiException(500, "Immigration instance not found.");
            }

            if (!immigration.ImmigrantsAvailable)
            {
                return new ExecutionResult { Success = true, Message = "No immigrants available.", Data = new { choices = new object[0] } };
            }

            var choices = new List<object>();
            // The game now provides one option at a time, which can be a duplicant or a care package.
            // We can't get a list of all options anymore. We'll simulate three choices.
            for (int i = 0; i < 3; i++)
            {
                var packageInfo = immigration.RandomCarePackage();
                if (packageInfo.id == null) // It's a duplicant
                {
                    var minion = new MinionStartingStats(false);
                    var traits = minion.Traits.Select(t => new { id = t.Id, name = t.Name, description = t.GetTooltip() }).ToList();
                    choices.Add(new { type = "duplicant", name = minion.Name, traits = traits, choice_info = "duplicant" });
                }
                else
                {
                    choices.Add(new { type = "item", info = packageInfo.id, amount = packageInfo.quantity, choice_info = packageInfo.id });
                }
            }

            return new ExecutionResult { Success = true, Message = "Printing pod choices retrieved.", Data = new { choices = choices } };
        }

        private static ExecutionResult SelectPrintingPodChoice(int worldId, JObject payload)
        {
            if (payload == null)
            {
                throw new ApiException(400, "Payload cannot be null.");
            }

            var choiceIndexToken = payload["choice_index"];
            if (choiceIndexToken == null || choiceIndexToken.Type != JTokenType.Integer)
            {
                throw new ApiException(400, "Payload must contain an integer 'choice_index' field.");
            }
            var choiceIndex = choiceIndexToken.ToObject<int>();

            var telepad = Components.Telepads.GetWorldItems(worldId).FirstOrDefault();
            if (telepad == null)
            {
                throw new ApiException(500, $"Telepad instance not found in world {worldId}.");
            }

            var immigration = Immigration.Instance;
            if (immigration == null)
            {
                throw new ApiException(500, "Immigration instance not found.");
            }

            if (!immigration.ImmigrantsAvailable)
            {
                throw new ApiException(400, "No immigrants available to choose from.");
            }

            // Re-fetch the choices to find the selected one, as we can't store them.
            var choices = new List<ITelepadDeliverable>();
            for (int i = 0; i < 3; i++)
            {
                choices.Add(immigration.RandomCarePackage());
            }

            if (choiceIndex < 0 || choiceIndex >= choices.Count)
            {
                throw new ApiException(400, $"Invalid choice_index: {choiceIndex}. Must be between 0 and {choices.Count - 1}.");
            }

            telepad.OnAcceptDelivery(choices[choiceIndex]);

            return new ExecutionResult { Success = true, Message = $"Choice {choiceIndex} selected." };
        }

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