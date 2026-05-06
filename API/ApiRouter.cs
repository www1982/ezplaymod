using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using EZPlay.API.Executors;
using EZPlay.API.Queries;

namespace EZPlay.API
{
    public static class ApiRouter
    {
        public delegate object ActionDelegate(string action, int worldId, JObject payload, string payloadString);

        private static readonly Dictionary<string, ActionDelegate> ExactRoutes = new Dictionary<string, ActionDelegate>
        {
            { "find_objects", (a, w, j, s) => FindObjectsQueryExecutor.Execute(w, s) },
            { "grid", (a, w, j, s) => GridQueryExecutor.Execute(w, s) },
            { "pathfinding", (a, w, j, s) => PathfindingQueryExecutor.Execute(w, s) },
            { "chore_status", (a, w, j, s) => ChoreStatusQueryExecutor.Execute(w, s) },
            { "execute_global_action", (a, w, j, s) => GlobalActionExecutor.Execute(w, s) },
            { "execute_reflection", (a, w, j, s) => ReflectionExecutor.Execute(w, s) },
            { "destroy_building", (a, w, j, s) => BuildingDestroyer.Execute(w, s) },
            { "Global.Dig", (a, w, j, s) => DigExecutor.Execute(w, s) },
            { "Global.Build", (a, w, j, s) => BuildExecutor.Execute(w, s) },
            { "/api/logistics/set_policy", (a, w, j, s) => LogisticsExecutor.SetPolicy(w, s) },
            { "/api/logistics/remove_policy", (a, w, j, s) => LogisticsExecutor.RemovePolicy(w, s) },
        };

        private static readonly Dictionary<string, ActionDelegate> PrefixRoutes = new Dictionary<string, ActionDelegate>
        {
            { "Duplicant.", (a, w, j, s) => DuplicantExecutor.HandleDuplicantAction(a, w, j) },
            { "Schedule.", (a, w, j, s) => ScheduleExecutor.HandleScheduleAction(a, w, j) },
            { "PrintingPod.", (a, w, j, s) => PrintingPodExecutor.HandlePrintingPodAction(w, j) },
            { "Research.", (a, w, j, s) => ResearchExecutor.HandleResearchAction(a, w, j) },
            { "Building.", (a, w, j, s) => BuildingExecutor.HandleBuildingAction(a, w, j) },
        };

        public static object Route(string action, int worldId, JObject payload, string payloadString)
        {
            if (ExactRoutes.TryGetValue(action, out var handler))
            {
                return handler(action, worldId, payload, payloadString);
            }

            foreach (var kvp in PrefixRoutes)
            {
                if (action.StartsWith(kvp.Key))
                {
                    return kvp.Value(action, worldId, payload, payloadString);
                }
            }

            throw new ArgumentException($"Unknown action received: {action}");
        }
    }
}
