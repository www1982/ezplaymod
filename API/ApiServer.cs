using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using EZPlay.API.Executors;
using EZPlay.API.Queries;
using EZPlay.Blueprints;
using EZPlay.GameState;
using EZPlay.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EZPlay.API
{
    public class ApiServer
    {
        private readonly HttpListener _listener = new HttpListener();
        private Thread _listenerThread;
        private readonly string _prefix;

        public ApiServer(string prefix) { _prefix = prefix; _listener.Prefixes.Add(prefix); }
        public void Start()
        {
            _listener.Start();
            _listenerThread = new Thread(() =>
            {
                try { while (_listener.IsListening) HandleRequest(_listener.GetContext()); }
                catch (HttpListenerException) { }
            })
            { IsBackground = true };
            _listenerThread.Start();
            Debug.Log($"[AI Mod] Universal API Server started. Listening on {_prefix}");
        }
        public void Stop() { _listener.Stop(); _listenerThread?.Join(); }

        private static class Routes
        {
            public const string ApiRoot = "/api";
            public const string State = "/state";
            public const string FindObjects = "/find_objects";
            public const string GetProperty = "/get_property";
            public const string CallMethod = "/call_method";
            public const string GetObjectDetails = "/get_object_details";
            public const string GetPath = "/pathfinding/get_path";
            public const string CreateBlueprint = "/blueprints/create";
            public const string PlaceBlueprint = "/blueprints/place";
            public const string Build = "/build";
            public const string Dig = "/dig";
            public const string Research = "/research";
            public const string GetCells = "/grid/get_cells";
            public const string GetChoresStatus = "/chores/get_status";
            public const string CoordsToCell = "/util/coords_to_cell";
            public const string CanBuild = "/precheck/build";
            public const string CanDig = "/precheck/dig";
        }

        private async void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            object responseData = null;
            string status = "success";
            int statusCode = (int)HttpStatusCode.OK;

            try
            {
                string body = new StreamReader(request.InputStream).ReadToEnd();
                string path = request.Url.AbsolutePath.ToLower();

                // 为根路径 /api/ 或 /api 提供一个欢迎页面和端点列表
                if (path == Routes.ApiRoot || path == Routes.ApiRoot + "/")
                {
                    responseData = new
                    {
                        message = "ONI Universal API Server is running.",
                        available_endpoints = new[]
                        {
                            $"GET  {Routes.ApiRoot}{Routes.State} - Retrieves the current game state for all worlds.",
                            $"POST {Routes.ApiRoot}{Routes.FindObjects} - Finds game objects. Body: {{ worldId: int, componentName: string }}",
                            $"POST {Routes.ApiRoot}{Routes.GetProperty} - Gets a property from an object. Body: {{ objectId: string, component: string, property: string }}",
                            $"POST {Routes.ApiRoot}{Routes.CallMethod} - Calls a method on an object. Body: {{ objectId: string, component: string, method: string, params: [...] }}",
                            $"POST {Routes.ApiRoot}{Routes.GetObjectDetails} - Gets all whitelisted details for an object. Body: {{ objectId: string }}",
                            $"POST {Routes.ApiRoot}{Routes.GetPath} - Gets pathfinding info between an object and a cell. Body: {{ navigatorId: string, targetCell: int }}",
                            $"POST {Routes.ApiRoot}{Routes.CreateBlueprint} - Creates and saves a blueprint. Body: {{ name: string, objectIds: [string], anchorObjectId: string }}",
                            $"POST {Routes.ApiRoot}{Routes.PlaceBlueprint} - Places a saved blueprint. Body: {{ name: string, anchorCell: int }}",
                            $"POST {Routes.ApiRoot}{Routes.Build} - Places a build order. Body: {{ buildingId: string, cell: int }}",
                            $"POST {Routes.ApiRoot}{Routes.Dig} - Places dig orders. Body: {{ cells: [int, ...] }}",
                            $"POST {Routes.ApiRoot}{Routes.Research} - Manages research. Body: {{ techId: string }} or {{ cancel: true }}",
                            $"POST {Routes.ApiRoot}{Routes.GetCells} - Gets detailed information for a list of cells. Body: {{ cells: [int, ...] }}",
                            $"POST {Routes.ApiRoot}{Routes.GetChoresStatus} - Gets the status of chores on a list of cells. Body: {{ cells: [int, ...] }}",
                            $"POST {Routes.ApiRoot}{Routes.CoordsToCell} - Converts XY coordinates to a cell ID. Body: {{ x: int, y: int }}",
                            $"POST {Routes.ApiRoot}{Routes.CanBuild} - Checks if a building can be placed. Body: {{ buildingId: string, cell: int }}",
                            $"POST {Routes.ApiRoot}{Routes.CanDig} - Checks if cells can be dug. Body: {{ cells: [int, ...] }}"
                        }
                    };
                }
                else if (path.EndsWith(Routes.GetPath))
                {
                    JObject query = JObject.Parse(body);
                    responseData = await MainThreadDispatcher.RunOnMainThread(() => PathfindingQueryExecutor.GetPath(query["navigatorId"].Value<string>(), query["targetCell"].Value<int>()));
                }
                else if (path.EndsWith(Routes.CreateBlueprint))
                {
                    JObject query = JObject.Parse(body);
                    var name = query["name"].Value<string>();
                    var objectIds = query["objectIds"].ToObject<List<string>>();
                    var anchorObjectId = query["anchorObjectId"].Value<string>();

                    var blueprint = await MainThreadDispatcher.RunOnMainThread(() => BlueprintManager.CreateBlueprint(name, objectIds, anchorObjectId));
                    BlueprintManager.SaveBlueprintToFile(blueprint);
                    responseData = $"Blueprint '{name}' created and saved successfully.";
                }
                else if (path.EndsWith(Routes.PlaceBlueprint))
                {
                    JObject query = JObject.Parse(body);
                    var name = query["name"].Value<string>();
                    var anchorCell = query["anchorCell"].Value<int>();

                    var blueprint = BlueprintManager.LoadBlueprintFromFile(name);
                    await MainThreadDispatcher.RunOnMainThread(() => BlueprintPlacer.PlaceBlueprint(blueprint, anchorCell));
                    responseData = $"Blueprint '{name}' placement commands issued.";
                }
                else if (path.EndsWith(Routes.GetObjectDetails))
                {
                    JObject query = JObject.Parse(body);
                    responseData = await MainThreadDispatcher.RunOnMainThread(() => ReflectionExecutor.GetObjectDetails(query["objectId"].Value<string>()));
                }
                else if (path.EndsWith(Routes.CanBuild))
                {
                    JObject query = JObject.Parse(body);
                    responseData = await MainThreadDispatcher.RunOnMainThread(() => GlobalActionExecutor.CanBuild(query["buildingId"].Value<string>(), query["cell"].Value<int>()));
                }
                else if (path.EndsWith(Routes.CanDig))
                {
                    JObject query = JObject.Parse(body);
                    var cells = query["cells"].ToObject<List<int>>();
                    responseData = await MainThreadDispatcher.RunOnMainThread(() => GlobalActionExecutor.CanDig(cells));
                }
                else if (path.EndsWith(Routes.GetChoresStatus))
                {
                    JObject query = JObject.Parse(body);
                    var cells = query["cells"].ToObject<List<int>>();
                    responseData = await MainThreadDispatcher.RunOnMainThread(() => ChoreStatusQueryExecutor.GetChoresStatus(cells));
                }
                else if (path.EndsWith(Routes.GetCells))
                {
                    JObject query = JObject.Parse(body);
                    var cells = query["cells"].ToObject<List<int>>();
                    responseData = await MainThreadDispatcher.RunOnMainThread(() => GridQueryExecutor.GetCellsInfo(cells));
                }
                else if (path.EndsWith(Routes.Build))
                {
                    JObject query = JObject.Parse(body);
                    await MainThreadDispatcher.RunOnMainThread(() => GlobalActionExecutor.Build(query["buildingId"].Value<string>(), query["cell"].Value<int>(), Orientation.Neutral, null));
                    responseData = "Build command issued.";
                }
                else if (path.EndsWith(Routes.Dig))
                {
                    JObject query = JObject.Parse(body);
                    var cells = query["cells"].ToObject<List<int>>();
                    await MainThreadDispatcher.RunOnMainThread(() => GlobalActionExecutor.Dig(cells));
                    responseData = "Dig command issued for " + cells.Count + " cells.";
                }
                else if (path.EndsWith(Routes.Research))
                {
                    JObject query = JObject.Parse(body);
                    string techId = query["techId"]?.Value<string>();
                    bool cancel = query["cancel"]?.Value<bool>() ?? false;
                    await MainThreadDispatcher.RunOnMainThread(() => GlobalActionExecutor.ManageResearch(techId, cancel));
                    responseData = "Research command issued.";
                }
                else if (path.EndsWith(Routes.CoordsToCell))
                {
                    JObject query = JObject.Parse(body);
                    responseData = GlobalActionExecutor.CoordsToCell(query["x"].Value<int>(), query["y"].Value<int>());
                }
                else if (path.EndsWith(Routes.FindObjects))
                {
                    JObject query = JObject.Parse(body);
                    responseData = await MainThreadDispatcher.RunOnMainThread(() => ReflectionExecutor.FindObjects(query));
                }
                else if (path.EndsWith(Routes.GetProperty))
                {
                    JObject query = JObject.Parse(body);
                    responseData = await MainThreadDispatcher.RunOnMainThread(() => ReflectionExecutor.GetProperty(query["objectId"].Value<string>(), query["component"].Value<string>(), query["property"].Value<string>()));
                }
                else if (path.EndsWith(Routes.CallMethod))
                {
                    JObject query = JObject.Parse(body);
                    responseData = await MainThreadDispatcher.RunOnMainThread(() => ReflectionExecutor.CallMethod(query["objectId"].Value<string>(), query["component"].Value<string>(), query["method"].Value<string>(), query["params"] as JArray));
                }
                // 保留旧的 /state 端点，方便快速观察
                else if (request.HttpMethod == "GET" && path.EndsWith(Routes.State))
                {
                    responseData = GameStateManager.LastKnownState;
                }
                else
                {
                    throw new NotSupportedException($"Endpoint '{request.Url.AbsolutePath}' not supported.");
                }
            }
            catch (Exception e)
            {
                status = "error";
                statusCode = e is UnauthorizedAccessException ? 403 : (e is NotSupportedException ? 404 : 400);
                responseData = e.InnerException?.Message ?? e.Message;
            }
            finally
            {
                var responseJson = JsonConvert.SerializeObject(new { status, data = responseData });
                byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
                response.StatusCode = statusCode;
                response.ContentType = "application/json";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.Close();
            }
        }
    }
}