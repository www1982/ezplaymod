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
                if (payload is string sPayload && sPayload.TrimStart().StartsWith("{"))
                {
                    try
                    {
                        jObjectPayload = JObject.Parse(sPayload);
                    }
                    catch (JsonReaderException)
                    {
                        return CreateResponse(request, "error", $"Action '{action}' requires a valid JSON object payload, but received an invalid JSON string.");
                    }
                }
                else if (payload is string s)
                {
                    try
                    {
                        jObjectPayload = JObject.Parse(s);
                    }
                    catch (JsonReaderException)
                    {
                        return CreateResponse(request, "error", $"Action '{action}' requires a valid JSON object payload, but received a non-JSON string.");
                    }
                }

                if (jObjectPayload == null)
                {
                    return CreateResponse(request, "error", $"Action '{action}' requires a valid JObject payload.");
                }

                object result;
                if (action.StartsWith("Duplicant."))
                {
                    result = PersonnelExecutor.HandleDuplicantAction(action, jObjectPayload);
                }
                else if (action.StartsWith("Schedule."))
                {
                    result = PersonnelExecutor.HandleScheduleAction(action, jObjectPayload);
                }
                else if (action.StartsWith("PrintingPod."))
                {
                    result = PersonnelExecutor.HandlePrintingPodAction(action, jObjectPayload);
                }
                else if (action.StartsWith("Research."))
                {
                    result = PersonnelExecutor.HandleResearchAction(action, jObjectPayload);
                }
                else
                {
                    var payloadString = jObjectPayload.ToString();
                    switch (action)
                    {
                        // Queries
                        case "find_objects":
                            result = FindObjectsQueryExecutor.Execute(payloadString);
                            break;
                        case "grid":
                            result = GridQueryExecutor.Execute(payloadString);
                            break;
                        case "pathfinding":
                            result = PathfindingQueryExecutor.Execute(payloadString);
                            break;
                        case "chore_status":
                            result = ChoreStatusQueryExecutor.Execute(payloadString);
                            break;

                        // Executors
                        case "execute_global_action":
                            result = GlobalActionExecutor.Execute(payloadString);
                            break;
                        case "execute_reflection":
                            result = ReflectionExecutor.Execute(payloadString);
                            break;
                        case "place_blueprint":
                            var blueprint = JsonConvert.DeserializeObject<Blueprint>(payloadString);
                            result = BlueprintManager.PlaceBlueprint(blueprint);
                            break;
                        case "destroy_building":
                            result = BuildingDestroyer.Execute(payloadString);
                            break;
                        case "/api/logistics/set_policy":
                            result = LogisticsExecutor.SetPolicy(payloadString);
                            break;
                        case "/api/logistics/remove_policy":
                            result = LogisticsExecutor.RemovePolicy(payloadString);
                            break;
                        case "/api/blueprints/create_from_game":
                            result = BlueprintExecutor.CreateFromGame(payloadString);
                            break;

                        default:
                            return CreateResponse(request, "error", $"Unknown action received: {action}");
                    }
                }

                // Check if the result is already a complete response
                if (result is JObject resultJObject && resultJObject.TryGetValue("status", StringComparison.OrdinalIgnoreCase, out _))
                {
                    // If it's a pre-formatted response, just add the request ID and return it as-is.
                    // This is useful for executors that return complex, custom-structured responses.
                    var response = resultJObject.ToObject<ApiResponse>();
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
    }
}