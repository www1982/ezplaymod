using System;
using EZPlay.API.Models;
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
        private static readonly JsonSerializerSettings SafeSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };

        public ApiResponse HandleRequest(ApiRequest request)
        {
            try
            {
                var action = request.Action;
                var payload = request.Payload;

                // Handle actions that don't require a payload first.
                if (action == "state")
                {
                    return CreateResponse(request, "success", GameStateManager.LastKnownState);
                }

                // All other actions require a JObject payload.
                JObject jObjectPayload = null;
                if (payload != null && payload.GetType() == typeof(JObject))
                {
                    jObjectPayload = (JObject)payload;
                }
                else if (payload is string sPayload)
                {
                    try
                    {
                        jObjectPayload = string.IsNullOrWhiteSpace(sPayload) ? new JObject() : JObject.Parse(sPayload);
                    }
                    catch (JsonReaderException e)
                    {
                        return CreateResponse(request, "error", $"Action '{action}' requires a valid JSON object payload. Parse error: {e.Message}");
                    }
                }

                if (jObjectPayload == null)
                {
                    return CreateResponse(request, "error", $"Action '{action}' requires a valid JObject payload, but received type {payload?.GetType().Name ?? "null"}.");
                }

                object result = HandleAction(action, jObjectPayload);

                if (result == null)
                {
                    return CreateResponse(request, "error", "Action handler returned null.");
                }

                // Check if the result is already a complete response
                if (result is JObject resultJObject && resultJObject.TryGetValue("status", StringComparison.OrdinalIgnoreCase, out _))
                {
                    // If it's a pre-formatted response, just add the request ID and return it as-is.
                    // This is useful for executors that return complex, custom-structured responses.
                    var response = resultJObject.ToObject<ApiResponse>(JsonSerializer.Create(SafeSettings));
                    response.RequestId = request.RequestId;
                    return response;
                }

                return CreateResponse(request, "success", result);
            }
            catch (Exception ex)
            {
                return new ApiResponse
                {
                    Type = request.Action + ".Response",
                    RequestId = request.RequestId,
                    Status = "error",
                    Payload = new { message = ex.Message, stackTrace = ex.StackTrace }
                };
            }
        }

        private ApiResponse CreateResponse(ApiRequest request, string status, object payload)
        {
            return new ApiResponse
            {
                Type = request.Action + ".Response",
                RequestId = request.RequestId,
                Status = status,
                Payload = payload
            };
        }

        private object HandleAction(string action, JObject payload)
        {
            var payloadString = payload.ToString();

            if (action.StartsWith("Duplicant."))
            {
                var worldId = GetWorldId(payload);
                return PersonnelExecutor.HandleDuplicantAction(action, worldId, payload);
            }
            if (action.StartsWith("Schedule."))
            {
                var worldId = GetWorldId(payload);
                return PersonnelExecutor.HandleScheduleAction(action, worldId, payload);
            }
            if (action.StartsWith("PrintingPod."))
            {
                var worldId = GetWorldId(payload);
                return PrintingPodExecutor.HandlePrintingPodAction(worldId, payload);
            }
            if (action.StartsWith("Research."))
            {
                var worldId = GetWorldId(payload);
                return PersonnelExecutor.HandleResearchAction(action, worldId, payload);
            }

            switch (action)
            {
                // Queries
                case "find_objects":
                    return FindObjectsQueryExecutor.Execute(GetWorldId(payload), payloadString);
                case "grid":
                    return GridQueryExecutor.Execute(GetWorldId(payload), payloadString);
                case "pathfinding":
                    return PathfindingQueryExecutor.Execute(GetWorldId(payload), payloadString);
                case "chore_status":
                    return ChoreStatusQueryExecutor.Execute(GetWorldId(payload), payloadString);

                // Executors
                case "execute_global_action":
                    return GlobalActionExecutor.Execute(GetWorldId(payload), payloadString);
                case "execute_reflection":
                    return ReflectionExecutor.Execute(GetWorldId(payload), payloadString);
                case "place_blueprint":
                    var worldId = GetWorldId(payload);
                    var blueprintToken = payload["blueprint"];
                    if (blueprintToken == null)
                    {
                        throw new ArgumentException("Payload for 'place_blueprint' must contain a 'blueprint' object.");
                    }
                    var blueprint = blueprintToken.ToObject<Blueprint>(JsonSerializer.Create(SafeSettings));
                    return BlueprintManager.PlaceBlueprint(worldId, blueprint);
                case "destroy_building":
                    return BuildingDestroyer.Execute(GetWorldId(payload), payloadString);
                case "/api/logistics/set_policy":
                    return LogisticsExecutor.SetPolicy(GetWorldId(payload), payloadString);
                case "/api/logistics/remove_policy":
                    return LogisticsExecutor.RemovePolicy(GetWorldId(payload), payloadString);
                case "/api/blueprints/create_from_game":
                    return BlueprintExecutor.CreateFromGame(GetWorldId(payload), payloadString);

                default:
                    throw new ArgumentException($"Unknown action received: {action}");
            }
        }

        private int GetWorldId(JObject payload)
        {
            if (payload.TryGetValue("worldId", out var worldIdToken) && worldIdToken.Type == JTokenType.Integer)
            {
                return worldIdToken.Value<int>();
            }
            throw new ArgumentException("Request payload must include a 'worldId' integer field.");
        }
    }
}