using System;
using System.Collections.Generic;
using System.Linq;
using EZPlay.API.Models;
using EZPlay.API.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EZPlay.API.Executors
{
    public static class BuildingExecutor
    {
        public static object HandleBuildingAction(string action, int worldId, JObject payload)
        {
            switch (action)
            {
                case "Building.SetRecipeQueue":
                    return SetRecipeQueue(worldId, payload);
                case "Building.SetStorageFilter":
                    return SetStorageFilter(worldId, payload);
                case "Building.SetLogicThreshold":
                    return SetLogicThreshold(worldId, payload);
                default:
                    throw new ApiException(400, $"Unknown building action: {action}");
            }
        }

        private static GameObject GetBuildingAt(int worldId, JObject payload, out int cell)
        {
            if (payload == null) throw new ApiException(400, "Payload cannot be null.");
            
            var xToken = payload["x"];
            var yToken = payload["y"];
            if (xToken == null || yToken == null)
                throw new ApiException(400, "Payload must contain 'x' and 'y' coordinates.");

            int x = xToken.Value<int>();
            int y = yToken.Value<int>();

            cell = Grid.PosToCell(new Vector3(x, y, 0));
            if (!Grid.IsValidCellInWorld(cell, worldId))
            {
                throw new ApiException(400, $"Invalid coordinates ({x}, {y}) in world {worldId}.");
            }

            var building = Grid.Objects[cell, (int)ObjectLayer.Building];
            if (building == null || building.GetMyWorldId() != worldId)
            {
                throw new ApiException(404, $"No building found at ({x}, {y}) in world {worldId}.");
            }
            return building;
        }

        private static ExecutionResult SetRecipeQueue(int worldId, JObject payload)
        {
            var originalWorldId = ClusterManager.Instance.activeWorldId;
            try
            {
                ClusterManager.Instance.SetActiveWorld(worldId);
                var building = GetBuildingAt(worldId, payload, out int cell);

                var recipeIdToken = payload["recipe_id"];
                var countToken = payload["count"];
                if (recipeIdToken == null || countToken == null)
                    throw new ApiException(400, "Payload must contain 'recipe_id' and 'count'.");

                string recipeId = recipeIdToken.ToString();
                int count = countToken.Value<int>();

                var fabricator = building.GetComponent<ComplexFabricator>();
                if (fabricator == null)
                    throw new ApiException(400, "Building does not have a ComplexFabricator component.");

                var recipe = ComplexRecipeManager.Get().GetRecipe(recipeId);
                if (recipe == null)
                    throw new ApiException(404, $"Recipe '{recipeId}' not found.");

                int current = fabricator.GetRecipeQueueCount(recipe);
                int target = count;
                
                if (target == -1) target = ComplexFabricator.QUEUE_INFINITE;

                if (target != current)
                {
                    // Most reliable way is to use reflection to find the setter method, 
                    // or just modify the array and trigger the event if the setter doesn't exist.
                    var setMethod = typeof(ComplexFabricator).GetMethod("SetRecipeQueueCount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (setMethod != null)
                    {
                        var parameters = setMethod.GetParameters();
                        if (parameters.Length == 2 && parameters[0].ParameterType == typeof(ComplexRecipe))
                        {
                            setMethod.Invoke(fabricator, new object[] { recipe, target });
                        }
                        else if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string))
                        {
                            setMethod.Invoke(fabricator, new object[] { recipe.id, target });
                        }
                    }
                    else
                    {
                        // Fallback to looping if setter not found
                        if (target > current && target != ComplexFabricator.QUEUE_INFINITE)
                        {
                            for (int i = 0; i < target - current; i++) fabricator.IncrementRecipeQueueCount(recipe);
                        }
                        else if (target < current && current != ComplexFabricator.QUEUE_INFINITE)
                        {
                            for (int i = 0; i < current - target; i++) fabricator.DecrementRecipeQueueCount(recipe, false);
                        }
                        else if (target == ComplexFabricator.QUEUE_INFINITE)
                        {
                            // UI shift+click usually does this
                            for(int i = 0; i < 99; i++) fabricator.IncrementRecipeQueueCount(recipe);
                        }
                        else if (target == 0 && current == ComplexFabricator.QUEUE_INFINITE)
                        {
                            // clear infinite
                            fabricator.DecrementRecipeQueueCount(recipe, false);
                        }
                    }
                }

                return new ExecutionResult { Success = true, Message = $"Recipe queue for '{recipeId}' set to {count} on {building.GetProperName()}." };
            }
            finally
            {
                ClusterManager.Instance.SetActiveWorld(originalWorldId);
            }
        }

        private static ExecutionResult SetStorageFilter(int worldId, JObject payload)
        {
            var originalWorldId = ClusterManager.Instance.activeWorldId;
            try
            {
                ClusterManager.Instance.SetActiveWorld(worldId);
                var building = GetBuildingAt(worldId, payload, out int cell);

                var tagsToken = payload["allowed_tags"];
                if (tagsToken == null || tagsToken.Type != JTokenType.Array)
                    throw new ApiException(400, "Payload must contain an 'allowed_tags' array.");

                HashSet<Tag> tags = new HashSet<Tag>();
                foreach (var t in tagsToken)
                {
                    tags.Add(new Tag(t.ToString()));
                }

                var treeFilterable = building.GetComponent<TreeFilterable>();
                if (treeFilterable != null)
                {
                    treeFilterable.UpdateFilters(tags);
                    return new ExecutionResult { Success = true, Message = $"Storage filters updated for {building.GetProperName()}." };
                }

                var filterable = building.GetComponent<Filterable>();
                if (filterable != null)
                {
                    if (tags.Count > 0)
                    {
                        filterable.SelectedTag = tags.First();
                    }
                    return new ExecutionResult { Success = true, Message = $"Filter set to {filterable.SelectedTag} for {building.GetProperName()}." };
                }

                throw new ApiException(400, "Building does not have a TreeFilterable or Filterable component.");
            }
            finally
            {
                ClusterManager.Instance.SetActiveWorld(originalWorldId);
            }
        }

        private static ExecutionResult SetLogicThreshold(int worldId, JObject payload)
        {
            var originalWorldId = ClusterManager.Instance.activeWorldId;
            try
            {
                ClusterManager.Instance.SetActiveWorld(worldId);
                var building = GetBuildingAt(worldId, payload, out int cell);

                var valueToken = payload["slider_value"];
                if (valueToken == null)
                    throw new ApiException(400, "Payload must contain a 'slider_value'.");

                float val = valueToken.Value<float>();

                var slider = building.GetComponent<ISliderControl>();
                if (slider != null)
                {
                    slider.SetSliderValue(val, 0); // index 0 usually
                    return new ExecutionResult { Success = true, Message = $"Threshold set to {val} for {building.GetProperName()}." };
                }

                throw new ApiException(400, "Building does not have an ISliderControl component.");
            }
            finally
            {
                ClusterManager.Instance.SetActiveWorld(originalWorldId);
            }
        }
    }
}
