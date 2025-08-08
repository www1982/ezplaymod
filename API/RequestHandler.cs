using System;
using EZPlay.API.Executors;
using EZPlay.API.Queries;
using EZPlay.Blueprints;
using EZPlay.GameState;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EZPlay.API
{
    public class RequestHandler
    {
        public object HandleRequest(string action, object payload)
        {
            if (payload is JObject jObjectPayload)
            {
                var payloadString = jObjectPayload.ToString();
                switch (action)
                {
                    // Queries
                    case "find_objects":
                        return FindObjectsQueryExecutor.Execute(payloadString);
                    case "grid":
                        return GridQueryExecutor.Execute(payloadString);
                    case "pathfinding":
                        return PathfindingQueryExecutor.Execute(payloadString);
                    case "chore_status":
                        return ChoreStatusQueryExecutor.Execute(payloadString);

                    // Executors
                    case "execute_global_action":
                        return GlobalActionExecutor.Execute(payloadString);
                    case "execute_reflection":
                        return ReflectionExecutor.Execute(payloadString);
                    case "place_blueprint":
                        var blueprint = JsonConvert.DeserializeObject<Blueprint>(payloadString);
                        return BlueprintManager.PlaceBlueprint(blueprint);
                    case "destroy_building":
                        return BuildingDestroyer.Execute(payloadString);
                    case "/api/logistics/set_policy":
                        return LogisticsExecutor.SetPolicy(payloadString);
                    case "/api/logistics/remove_policy":
                        return LogisticsExecutor.RemovePolicy(payloadString);

                    default:
                        throw new ArgumentException($"Unknown action: {action}");
                }
            }

            // Handle cases where payload is not a JObject or is null
            switch (action)
            {
                case "state":
                    return GameStateManager.LastKnownState;
                default:
                    throw new ArgumentException($"Action '{action}' requires a valid payload.");
            }
        }
    }
}