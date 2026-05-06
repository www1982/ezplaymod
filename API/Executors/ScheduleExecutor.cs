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
    public static class ScheduleExecutor
    {
        private static EZPlay.Core.Interfaces.ILogger Logger => EZPlay.Core.ServiceContainer.Resolve<EZPlay.Core.Interfaces.ILogger>();
        
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
    }
}
