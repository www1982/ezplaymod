using HarmonyLib;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Klei.AI;
using KMod;

// =========================================================================
// I. 核心 MOD 加载与补丁
// =========================================================================

public class ModLoader : UserMod2
{
    private static ApiServer _apiServer;

    public override void OnLoad(Harmony harmony)
    {
        base.OnLoad(harmony);

        // API服务器现在监听 /api/ 根路径
        _apiServer = new ApiServer("http://localhost:8080/api/");
        _apiServer.Start();
    }
}

// 在加载新游戏时，清空对象缓存，避免ID冲突和内存泄漏
[HarmonyPatch(typeof(Game), "Load")]
public class GameLoadPatch
{
    public static void Postfix() => GameObjectManager.Clear();
}

// 每一帧都处理来自主线程调度器的任务队列
[HarmonyPatch(typeof(Game), "Update")]
public class DispatcherPatch
{
    public static void Postfix() => MainThreadDispatcher.ProcessQueue();
}

// 定期更新游戏状态缓存 (用于 /state 端点)
[HarmonyPatch(typeof(Game), "SimEveryTick")]
public class GameStateMonitorPatch
{
    private static float timer = 0f;
    private const float UPDATE_INTERVAL_SECONDS = 2f;
    public static void Postfix(float dt)
    {
        timer += dt;
        if (timer < UPDATE_INTERVAL_SECONDS) return;
        timer = 0f;
        GameStateManager.UpdateState();
    }
}


// =========================================================================
// II. 新的通用API核心组件
// =========================================================================

/// <summary>
/// 管理游戏对象的缓存，将 GameObject 实例映射到安全的字符串ID。
/// </summary>
public static class GameObjectManager
{
    // 使用 WeakReference 防止我们的缓存阻止游戏对象被垃圾回收
    private static readonly Dictionary<string, WeakReference<GameObject>> CachedObjects = new Dictionary<string, WeakReference<GameObject>>();

    public static string CacheObject(GameObject go)
    {
        if (go == null) return null;
        string id = Guid.NewGuid().ToString();
        CachedObjects[id] = new WeakReference<GameObject>(go);
        return id;
    }

    public static GameObject GetObject(string id)
    {
        if (CachedObjects.TryGetValue(id, out var weakRef))
        {
            if (weakRef.TryGetTarget(out var go)) return go;
            CachedObjects.Remove(id); // 对象已被销毁，从缓存移除
        }
        return null;
    }

    public static void Clear() => CachedObjects.Clear();
}


/// <summary>
/// 负责将来自其他线程的操作安全地调度到游戏主线程上执行，并能异步返回结果。
/// </summary>
public static class MainThreadDispatcher
{
    private static readonly Queue<System.Action> ExecutionQueue = new Queue<System.Action>();

    public static Task<T> RunOnMainThread<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();
        System.Action action = () =>
        {
            try { tcs.SetResult(func()); }
            catch (Exception e) { tcs.SetException(e); }
        };
        lock (ExecutionQueue) { ExecutionQueue.Enqueue(action); }
        return tcs.Task;
    }

    // 简化版，用于不需要返回值的操作 (现在返回Task以支持await)
    public static Task RunOnMainThread(System.Action act)
    {
        var tcs = new TaskCompletionSource<object>();
        System.Action action = () =>
        {
            try
            {
                act();
                tcs.SetResult(null);
            }
            catch (Exception e) { tcs.SetException(e); }
        };
        lock (ExecutionQueue) { ExecutionQueue.Enqueue(action); }
        return tcs.Task;
    }

    public static void ProcessQueue()
    {
        while (ExecutionQueue.Count > 0)
        {
            System.Action action;
            lock (ExecutionQueue) { action = ExecutionQueue.Dequeue(); }
            action.Invoke();
        }
    }
}


/// <summary>
/// 安全白名单，定义了AI可以通过反射API访问的组件和方法。
/// 这是保障游戏稳定的最重要防线。
/// </summary>
public static class SecurityWhitelist
{
    public static readonly HashSet<string> AllowedComponents = new HashSet<string>
    {
        "Storage", "PrimaryElement", "Prioritizable", "BuildingComplete", "TreeFilterable"
    };

    public static readonly HashSet<string> AllowedMethods = new HashSet<string>
    {
        // Prioritizable
        "Prioritizable.SetMasterPriority",
        // Storage
        "Storage.SetOnlyFetchMarkedItems",
        "Storage.allowItemRemoval",
        // TreeFilterable
        "TreeFilterable.AddTagToFilter",
        "TreeFilterable.RemoveTagFromFilter",
        "TreeFilterable.UpdateFilters"
    };

    public static readonly HashSet<string> AllowedProperties = new HashSet<string>
    {
        // PrimaryElement
        "PrimaryElement.Temperature",
        "PrimaryElement.Mass",
        "PrimaryElement.ElementID",
        // Storage
        "Storage.capacity",
        "Storage.MassStored",
        // Prioritizable
        "Prioritizable.masterPriority"
    };
}

// =========================================================================
// III. 新增的网格查询与全局指令执行器
// =========================================================================

/// <summary>
/// 封装单个游戏格子的详细信息，供API查询。
/// </summary>
public class CellInfo
{
    public int Cell { get; set; }
    public string ElementId { get; set; }
    public string ElementState { get; set; } // Solid, Liquid, Gas
    public float Mass { get; set; }
    public float Temperature { get; set; }
    public string DiseaseName { get; set; }
    public int DiseaseCount { get; set; }
    public List<string> GameObjects { get; set; } = new List<string>();
}

/// <summary>
/// 负责处理对游戏世界网格的查询。
/// </summary>
public static class GridQueryExecutor
{
    public static Dictionary<int, CellInfo> GetCellsInfo(List<int> cells)
    {
        var result = new Dictionary<int, CellInfo>();
        foreach (var cell in cells)
        {
            if (!Grid.IsValidCell(cell)) continue;

            var element = Grid.Element[cell];
            var info = new CellInfo
            {
                Cell = cell,
                ElementId = element.id.ToString(),
                ElementState = element.state.ToString(),
                Mass = Grid.Mass[cell],
                Temperature = Grid.Temperature[cell],
                DiseaseName = "None", // Default value
                DiseaseCount = Grid.DiseaseCount[cell]
            };

            byte diseaseIdx = Grid.DiseaseIdx[cell];
            if (diseaseIdx != 255 && diseaseIdx < Db.Get().Diseases.Count)
            {
                info.DiseaseName = Db.Get().Diseases[diseaseIdx].Id;
            }

            // 遍历所有可能的图层，查找格子上的对象
            for (int i = 0; i < (int)ObjectLayer.NumLayers; i++)
            {
                var go = Grid.Objects[cell, i];
                if (go != null)
                {
                    info.GameObjects.Add(go.GetProperName());
                }
            }

            result[cell] = info;
        }
        return result;
    }
}

/// <summary>
/// 负责处理不针对特定游戏对象的全局指令，如建造、挖掘、研究等。
/// </summary>
public static class GlobalActionExecutor
{
    // 1. 建造能力 (修正版, 返回GameObject)
    public static GameObject Build(string buildingId, int cell, Orientation orientation = Orientation.Neutral, List<Tag> selected_elements = null)
    {
        var def = Assets.GetBuildingDef(buildingId);
        if (def == null)
            throw new ArgumentException($"Building with ID '{buildingId}' not found.");

        var pos = Grid.CellToPosCBC(cell, def.SceneLayer);
        if (selected_elements == null)
        {
            selected_elements = def.DefaultElements();
        }
        if (orientation == Orientation.Neutral)
        {
            orientation = def.InitialOrientation;
        }

        // 直接调用BuildingDef的TryPlace方法，绕过UI工具
        var go = def.TryPlace(null, pos, orientation, selected_elements, null, 0);

        if (go == null)
        {
            // 尝试作为替换来建造
            var replacementCandidate = def.GetReplacementCandidate(cell);
            if (replacementCandidate != null)
            {
                go = def.TryReplaceTile(null, pos, orientation, selected_elements, null, 0);
            }
        }

        if (go == null)
            throw new Exception($"Failed to place building '{buildingId}' at cell {cell}. Location may be invalid or obstructed.");

        // 设置默认优先级
        var prioritizable = go.GetComponent<Prioritizable>();
        if (prioritizable != null)
        {
            prioritizable.SetMasterPriority(new PrioritySetting(PriorityScreen.PriorityClass.basic, 5));
        }
        return go;
    }

    // 2. 挖掘能力 (修正版)
    public static void Dig(List<int> cells)
    {
        if (cells == null || cells.Count == 0)
            throw new ArgumentException("'cells' list cannot be null or empty.");

        // 直接调用挖掘动作，而不是工具本身
        foreach (var cell in cells)
        {
            InterfaceTool.ActiveConfig.DigAction.Dig(cell, 0);
        }
    }

    // 3. 研究能力 (修正版)
    public static void ManageResearch(string techId, bool cancel = false)
    {
        var research = Research.Instance;
        if (research == null)
            throw new Exception("Research instance not found.");

        if (cancel)
        {
            var activeResearch = research.GetActiveResearch();
            if (activeResearch != null)
            {
                // 修正：CancelResearch需要一个Tech参数
                research.CancelResearch(activeResearch.tech);
            }
        }
        else
        {
            if (string.IsNullOrEmpty(techId))
                throw new ArgumentException("'techId' must be provided to set research.");

            var tech = Db.Get().Techs.Get(techId);
            if (tech == null)
                throw new ArgumentException($"Tech with ID '{techId}' not found.");

            research.SetActiveResearch(tech, true); // clearQueue=true to match player behavior
        }
    }

    // 4. 辅助工具：坐标转换 (修正版)
    public static int CoordsToCell(int x, int y)
    {
        int cell = Grid.XYToCell(x, y);
        // 修正：Grid没有IsValidXY，但我们可以通过检查cell是否有效来达到同样目的
        if (!Grid.IsValidCell(cell))
            throw new ArgumentException($"Invalid coordinates: ({x}, {y})");
        return cell;
    }

    // 5. 预检：建造
    public static bool CanBuild(string buildingId, int cell)
    {
        var def = Assets.GetBuildingDef(buildingId);
        if (def == null) return false;

        var orientation = def.InitialOrientation;
        string fail_reason;

        // 使用游戏内置的函数来检查是否可以建造
        return def.IsValidPlaceLocation(null, Grid.CellToPos(cell), orientation, out fail_reason);
    }

    // 6. 预检：挖掘
    public static Dictionary<int, bool> CanDig(List<int> cells)
    {
        var result = new Dictionary<int, bool>();
        if (cells == null) return result;

        foreach (var cell in cells)
        {
            if (!Grid.IsValidCell(cell) || !Grid.Solid[cell])
            {
                result[cell] = false;
                continue;
            }

            var go = Grid.Objects[cell, (int)ObjectLayer.FoundationTile];
            result[cell] = go != null && go.GetComponent<Diggable>() != null;
        }
        return result;
    }
}


/// <summary>
/// 反射执行器，负责解析API请求，并通过反射动态执行操作。
/// </summary>
public static class ReflectionExecutor
{
    // 查找对象 (已更新为世界感知)
    public static List<string> FindObjects(JObject query)
    {
        var componentName = query["componentName"]?.Value<string>();
        if (string.IsNullOrEmpty(componentName))
            throw new ArgumentException("'componentName' is required for find_objects.");

        if (query["worldId"] == null)
            throw new ArgumentException("'worldId' is required for find_objects.");
        var worldId = query["worldId"].Value<int>();

        var foundObjects = new List<string>();

        // 修正：使用正确的组件集合名称，并为没有直接集合的组件提供备用查找方案
        switch (componentName)
        {
            case "Prioritizable":
                foreach (var item in Components.Prioritizables.GetWorldItems(worldId))
                    foundObjects.Add(GameObjectManager.CacheObject(item.gameObject));
                break;
            case "BuildingComplete":
                foreach (var item in Components.BuildingCompletes.GetWorldItems(worldId))
                    foundObjects.Add(GameObjectManager.CacheObject(item.gameObject));
                break;

            // 对于没有专用集合的白名单组件，使用全局查找然后按世界ID过滤
            case "Storage":
                foreach (var item in UnityEngine.Object.FindObjectsOfType<Storage>())
                    if (item.GetMyWorldId() == worldId)
                        foundObjects.Add(GameObjectManager.CacheObject(item.gameObject));
                break;
            case "PrimaryElement":
                foreach (var item in UnityEngine.Object.FindObjectsOfType<PrimaryElement>())
                    if (item.GetMyWorldId() == worldId)
                        foundObjects.Add(GameObjectManager.CacheObject(item.gameObject));
                break;
            case "TreeFilterable":
                foreach (var item in UnityEngine.Object.FindObjectsOfType<TreeFilterable>())
                    if (item.GetMyWorldId() == worldId)
                        foundObjects.Add(GameObjectManager.CacheObject(item.gameObject));
                break;

            default:
                // 对于不在白名单中的组件，不执行任何操作以确保安全
                break;
        }

        return foundObjects;
    }

    // 获取属性
    public static object GetProperty(string objectId, string componentName, string propertyName)
    {
        var go = GameObjectManager.GetObject(objectId);
        if (go == null) throw new Exception($"Object with ID '{objectId}' not found.");

        if (!SecurityWhitelist.AllowedComponents.Contains(componentName) || !SecurityWhitelist.AllowedProperties.Contains($"{componentName}.{propertyName}"))
            throw new UnauthorizedAccessException($"Access to property '{componentName}.{propertyName}' is denied.");

        var component = go.GetComponent(componentName);
        if (component == null) throw new Exception($"Component '{componentName}' not found.");

        var propInfo = component.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (propInfo == null) throw new Exception($"Property '{propertyName}' not found.");

        return propInfo.GetValue(component, null);
    }

    // 调用方法
    public static object CallMethod(string objectId, string componentName, string methodName, JArray args)
    {
        var go = GameObjectManager.GetObject(objectId);
        if (go == null) throw new Exception($"Object with ID '{objectId}' not found.");

        if (!SecurityWhitelist.AllowedComponents.Contains(componentName) || !SecurityWhitelist.AllowedMethods.Contains($"{componentName}.{methodName}"))
            throw new UnauthorizedAccessException($"Access to method '{componentName}.{methodName}' is denied.");

        var component = go.GetComponent(componentName);
        if (component == null) throw new Exception($"Component '{componentName}' not found.");

        // 简化的方法查找，未处理重载
        var methodInfo = component.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        if (methodInfo == null) throw new Exception($"Method '{methodName}' not found.");

        var methodParams = methodInfo.GetParameters();
        if (args.Count != methodParams.Length) throw new Exception("Incorrect number of parameters.");

        object[] convertedArgs = new object[args.Count];
        for (int i = 0; i < args.Count; i++)
        {
            convertedArgs[i] = ConvertJTokenToType(args[i], methodParams[i].ParameterType);
        }

        return methodInfo.Invoke(component, convertedArgs);
    }

    // 获取对象所有白名单内的详细信息
    public static Dictionary<string, Dictionary<string, object>> GetObjectDetails(string objectId)
    {
        var go = GameObjectManager.GetObject(objectId);
        if (go == null) throw new Exception($"Object with ID '{objectId}' not found.");

        var result = new Dictionary<string, Dictionary<string, object>>();

        foreach (var componentName in SecurityWhitelist.AllowedComponents)
        {
            var component = go.GetComponent(componentName);
            if (component == null) continue;

            var componentProperties = new Dictionary<string, object>();
            var properties = component.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var propInfo in properties)
            {
                string fullPropName = $"{componentName}.{propInfo.Name}";
                if (SecurityWhitelist.AllowedProperties.Contains(fullPropName))
                {
                    try
                    {
                        componentProperties[propInfo.Name] = propInfo.GetValue(component, null);
                    }
                    catch { } // Ignore properties that might fail to get
                }
            }

            if (componentProperties.Count > 0)
            {
                result[componentName] = componentProperties;
            }
        }

        return result;
    }

    // 简化的类型转换器
    private static object ConvertJTokenToType(JToken token, Type targetType)
    {
        if (targetType == typeof(PrioritySetting))
        {
            var priorityClass = (PriorityScreen.PriorityClass)Enum.Parse(typeof(PriorityScreen.PriorityClass), token["priority_class"].Value<string>());
            var priorityValue = token["priority_value"].Value<int>();
            return new PrioritySetting(priorityClass, priorityValue);
        }
        if (targetType == typeof(Tag))
        {
            return new Tag(token.Value<string>());
        }
        return token.ToObject(targetType);
    }
}


// =========================================================================
// III. 全面升级的 API 服务器
// =========================================================================

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
            if (path == "/api/" || path == "/api")
            {
                responseData = new
                {
                    message = "ONI Universal API Server is running.",
                    available_endpoints = new[]
                    {
                        "GET  /api/state - Retrieves the current game state for all worlds.",
                        "POST /api/find_objects - Finds game objects. Body: { worldId: int, componentName: string }",
                        "POST /api/get_property - Gets a property from an object. Body: { objectId: string, component: string, property: string }",
                        "POST /api/call_method - Calls a method on an object. Body: { objectId: string, component: string, method: string, params: [...] }",
                        "POST /api/get_object_details - Gets all whitelisted details for an object. Body: { objectId: string }",
                        "POST /api/pathfinding/get_path - Gets pathfinding info between an object and a cell. Body: { navigatorId: string, targetCell: int }",
                        "POST /api/blueprints/create - Creates and saves a blueprint. Body: { name: string, objectIds: [string], anchorObjectId: string }",
                        "POST /api/blueprints/place - Places a saved blueprint. Body: { name: string, anchorCell: int }",
                        "POST /api/build - Places a build order. Body: { buildingId: string, cell: int }",
                        "POST /api/dig - Places dig orders. Body: { cells: [int, ...] }",
                        "POST /api/research - Manages research. Body: { techId: string } or { cancel: true }",
                        "POST /api/grid/get_cells - Gets detailed information for a list of cells. Body: { cells: [int, ...] }",
                        "POST /api/chores/get_status - Gets the status of chores on a list of cells. Body: { cells: [int, ...] }",
                        "POST /api/util/coords_to_cell - Converts XY coordinates to a cell ID. Body: { x: int, y: int }",
                        "POST /api/precheck/build - Checks if a building can be placed. Body: { buildingId: string, cell: int }",
                        "POST /api/precheck/dig - Checks if cells can be dug. Body: { cells: [int, ...] }"
                    }
                };
            }
            else if (path.EndsWith("/pathfinding/get_path"))
            {
                JObject query = JObject.Parse(body);
                responseData = await MainThreadDispatcher.RunOnMainThread(() => PathfindingQueryExecutor.GetPath(query["navigatorId"].Value<string>(), query["targetCell"].Value<int>()));
            }
            else if (path.EndsWith("/blueprints/create"))
            {
                JObject query = JObject.Parse(body);
                var name = query["name"].Value<string>();
                var objectIds = query["objectIds"].ToObject<List<string>>();
                var anchorObjectId = query["anchorObjectId"].Value<string>();

                var blueprint = await MainThreadDispatcher.RunOnMainThread(() => BlueprintManager.CreateBlueprint(name, objectIds, anchorObjectId));
                BlueprintManager.SaveBlueprintToFile(blueprint);
                responseData = $"Blueprint '{name}' created and saved successfully.";
            }
            else if (path.EndsWith("/blueprints/place"))
            {
                JObject query = JObject.Parse(body);
                var name = query["name"].Value<string>();
                var anchorCell = query["anchorCell"].Value<int>();

                var blueprint = BlueprintManager.LoadBlueprintFromFile(name);
                await MainThreadDispatcher.RunOnMainThread(() => BlueprintPlacer.PlaceBlueprint(blueprint, anchorCell));
                responseData = $"Blueprint '{name}' placement commands issued.";
            }
            else if (path.EndsWith("/get_object_details"))
            {
                JObject query = JObject.Parse(body);
                responseData = await MainThreadDispatcher.RunOnMainThread(() => ReflectionExecutor.GetObjectDetails(query["objectId"].Value<string>()));
            }
            else if (path.EndsWith("/precheck/build"))
            {
                JObject query = JObject.Parse(body);
                responseData = await MainThreadDispatcher.RunOnMainThread(() => GlobalActionExecutor.CanBuild(query["buildingId"].Value<string>(), query["cell"].Value<int>()));
            }
            else if (path.EndsWith("/precheck/dig"))
            {
                JObject query = JObject.Parse(body);
                var cells = query["cells"].ToObject<List<int>>();
                responseData = await MainThreadDispatcher.RunOnMainThread(() => GlobalActionExecutor.CanDig(cells));
            }
            else if (path.EndsWith("/chores/get_status"))
            {
                JObject query = JObject.Parse(body);
                var cells = query["cells"].ToObject<List<int>>();
                responseData = await MainThreadDispatcher.RunOnMainThread(() => ChoreStatusQueryExecutor.GetChoresStatus(cells));
            }
            else if (path.EndsWith("/grid/get_cells"))
            {
                JObject query = JObject.Parse(body);
                var cells = query["cells"].ToObject<List<int>>();
                responseData = await MainThreadDispatcher.RunOnMainThread(() => GridQueryExecutor.GetCellsInfo(cells));
            }
            else if (path.EndsWith("/build"))
            {
                JObject query = JObject.Parse(body);
                await MainThreadDispatcher.RunOnMainThread(() => GlobalActionExecutor.Build(query["buildingId"].Value<string>(), query["cell"].Value<int>(), Orientation.Neutral, null));
                responseData = "Build command issued.";
            }
            else if (path.EndsWith("/dig"))
            {
                JObject query = JObject.Parse(body);
                var cells = query["cells"].ToObject<List<int>>();
                await MainThreadDispatcher.RunOnMainThread(() => GlobalActionExecutor.Dig(cells));
                responseData = "Dig command issued for " + cells.Count + " cells.";
            }
            else if (path.EndsWith("/research"))
            {
                JObject query = JObject.Parse(body);
                string techId = query["techId"]?.Value<string>();
                bool cancel = query["cancel"]?.Value<bool>() ?? false;
                await MainThreadDispatcher.RunOnMainThread(() => GlobalActionExecutor.ManageResearch(techId, cancel));
                responseData = "Research command issued.";
            }
            else if (path.EndsWith("/util/coords_to_cell"))
            {
                JObject query = JObject.Parse(body);
                responseData = GlobalActionExecutor.CoordsToCell(query["x"].Value<int>(), query["y"].Value<int>());
            }
            else if (path.EndsWith("/find_objects"))
            {
                JObject query = JObject.Parse(body);
                responseData = await MainThreadDispatcher.RunOnMainThread(() => ReflectionExecutor.FindObjects(query));
            }
            else if (path.EndsWith("/get_property"))
            {
                JObject query = JObject.Parse(body);
                responseData = await MainThreadDispatcher.RunOnMainThread(() => ReflectionExecutor.GetProperty(query["objectId"].Value<string>(), query["component"].Value<string>(), query["property"].Value<string>()));
            }
            else if (path.EndsWith("/call_method"))
            {
                JObject query = JObject.Parse(body);
                responseData = await MainThreadDispatcher.RunOnMainThread(() => ReflectionExecutor.CallMethod(query["objectId"].Value<string>(), query["component"].Value<string>(), query["method"].Value<string>(), query["params"] as JArray));
            }
            // 保留旧的 /state 端点，方便快速观察
            else if (request.HttpMethod == "GET" && path.EndsWith("/state"))
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


// =========================================================================
// IV. 观察系统 (已升级为多世界支持)
// =========================================================================

// 全局殖民地状态
public class ColonyState
{
    public int Cycle { get; set; }
    public float TimeInCycle { get; set; }
    public List<TechData> ResearchState { get; set; } = new List<TechData>();
    public DailyReportData LatestDailyReport { get; set; }
    public Dictionary<int, WorldState> Worlds { get; set; } = new Dictionary<int, WorldState>();
}

// 单个世界(星球)的状态
public class WorldState
{
    public int WorldId { get; set; }
    public string WorldName { get; set; }
    public Dictionary<string, float> Resources { get; set; } = new Dictionary<string, float>();
    public List<DuplicantState> Duplicants { get; set; } = new List<DuplicantState>();
    public ColonySummaryData ColonySummary { get; set; }
}

public class DuplicantState
{
    public string Name { get; set; }
    public float Stress { get; set; }
    public float Health { get; set; }
}

public class TechData
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Status { get; set; }
    public float ProgressPercent { get; set; }
}

public class ColonySummaryData
{
    public int DuplicantCount { get; set; }
    public float Calories { get; set; }
    public Dictionary<string, int> CritterCount { get; set; } = new Dictionary<string, int>();
}

public class DailyReportData
{
    public int CycleNumber { get; set; }
    public float OxygenChange { get; set; }
    public float CalorieChange { get; set; }
    public float PowerChange { get; set; }
}


public static class GameStateManager
{
    public static ColonyState LastKnownState { get; private set; }
    public static void UpdateState()
    {
        if (SaveLoader.Instance == null || !SaveLoader.Instance.loadedFromSave || ClusterManager.Instance == null) return;

        var colonyState = new ColonyState
        {
            Cycle = GameClock.Instance.GetCycle(),
            TimeInCycle = GameClock.Instance.GetTimeInCycles()
        };

        // --- 全局研究进度 ---
        if (Research.Instance != null && Db.Get().Techs != null)
        {
            var activeResearch = Research.Instance.GetActiveResearch();
            foreach (var tech in Db.Get().Techs.resources)
            {
                var techInstance = Research.Instance.GetTechInstance(tech.Id);
                string status = "Locked";
                if (techInstance != null && techInstance.IsComplete()) status = "Completed";
                else if (tech.ArePrerequisitesComplete()) status = "Available";

                colonyState.ResearchState.Add(new TechData
                {
                    Id = tech.Id,
                    Name = tech.Name,
                    Status = status,
                    ProgressPercent = (activeResearch != null && activeResearch.tech == tech) ? activeResearch.GetTotalPercentageComplete() : 0f
                });
            }
        }

        // --- 全局最新一份每日报告 ---
        var reportManager = ReportManager.Instance;
        if (reportManager != null && reportManager.reports.Count > 0)
        {
            var latestReport = reportManager.reports[reportManager.reports.Count - 1];
            colonyState.LatestDailyReport = new DailyReportData
            {
                CycleNumber = latestReport.day,
                OxygenChange = latestReport.GetEntry(ReportManager.ReportType.OxygenCreated).Net,
                CalorieChange = latestReport.GetEntry(ReportManager.ReportType.CaloriesCreated).Net,
                PowerChange = latestReport.GetEntry(ReportManager.ReportType.EnergyCreated).Net
            };
        }

        // --- 按星球分类收集数据 ---
        foreach (var world in ClusterManager.Instance.WorldContainers)
        {
            if (!world.IsDiscovered) continue;

            var worldState = new WorldState
            {
                WorldId = world.id,
                WorldName = world.GetProperName()
            };

            var inventory = world.worldInventory;

            // --- 资源 ---
            foreach (Element element in ElementLoader.elements)
            {
                if (element != null && element.id != SimHashes.Vacuum)
                {
                    float amount = inventory.GetAmountWithoutTag(element.tag);
                    if (amount > 0) worldState.Resources[element.name] = amount;
                }
            }

            // --- 复制人 ---
            foreach (var minion in Components.MinionIdentities.GetWorldItems(world.id))
            {
                if (minion == null) continue;
                var dupeState = new DuplicantState { Name = minion.name.Replace("(Clone)", "") };
                dupeState.Stress = minion.GetAmounts().Get(Db.Get().Amounts.Stress)?.value ?? 0;
                dupeState.Health = minion.GetComponent<Health>()?.hitPoints ?? 0;
                worldState.Duplicants.Add(dupeState);
            }

            // --- 殖民地概要 (按星球) ---
            worldState.ColonySummary = new ColonySummaryData
            {
                DuplicantCount = Components.MinionIdentities.GetWorldItems(world.id).Count,
                Calories = inventory.GetAmountWithoutTag(GameTags.Edible)
            };
            foreach (Brain brain in Components.Brains.GetWorldItems(world.id))
            {
                if (brain != null && brain.gameObject != null && !brain.gameObject.HasTag(GameTags.BaseMinion))
                {
                    string name = brain.gameObject.GetProperName();
                    if (worldState.ColonySummary.CritterCount.ContainsKey(name)) worldState.ColonySummary.CritterCount[name]++;
                    else worldState.ColonySummary.CritterCount[name] = 1;
                }
            }

            colonyState.Worlds[world.id] = worldState;
        }

        LastKnownState = colonyState;
    }
}


// =========================================================================
// V. 新增的路径查询 API
// =========================================================================

/// <summary>
/// 封装寻路查询的结果。
/// </summary>
public class PathInfo
{
    public bool Reachable { get; set; }
    public int Cost { get; set; }
    public List<int> Path { get; set; }
}

/// <summary>
/// 负责处理寻路和可达性查询。
/// </summary>
public static class PathfindingQueryExecutor
{
    public static PathInfo GetPath(string navigatorId, int targetCell)
    {
        var go = GameObjectManager.GetObject(navigatorId);
        if (go == null) throw new Exception($"Navigator object with ID '{navigatorId}' not found.");

        var navigator = go.GetComponent<Navigator>();
        if (navigator == null) throw new Exception($"Object with ID '{navigatorId}' does not have a Navigator component.");

        var info = new PathInfo { Path = new List<int>() };

        int cost = navigator.GetNavigationCost(targetCell);
        info.Cost = cost;
        info.Reachable = cost != -1;

        if (info.Reachable)
        {
            var query = PathFinderQueries.cellQuery.Reset(targetCell);
            var path = new PathFinder.Path();

            int startCell = Grid.PosToCell(navigator.gameObject);
            var potentialPath = new PathFinder.PotentialPath(startCell, navigator.CurrentNavType, navigator.flags);

            PathFinder.Run(navigator.NavGrid, navigator.GetCurrentAbilities(), potentialPath, query, ref path);

            if (path.IsValid())
            {
                foreach (var node in path.nodes)
                {
                    info.Path.Add(node.cell);
                }
            }
        }

        return info;
    }
}


// =========================================================================
// VI. 新增的任务状态查询 API
// =========================================================================

/// <summary>
/// 封装单个待办事项(Chore)的状态信息。
/// </summary>
public class ChoreStatusInfo
{
    public string ChoreType { get; set; }
    public string Status { get; set; } // e.g., "Pending", "In Progress", "Completed"
    public string Assignee { get; set; } // Name of the duplicant assigned, or null
    public int Priority { get; set; }
    public string PriorityClass { get; set; }
    public bool IsReachable { get; set; } // Is the chore reachable by any duplicant
}

/// <summary>
/// 负责处理对游戏世界中待办事项(Chores)状态的查询。
/// </summary>
public static class ChoreStatusQueryExecutor
{
    public static Dictionary<int, List<ChoreStatusInfo>> GetChoresStatus(List<int> cells)
    {
        var result = new Dictionary<int, List<ChoreStatusInfo>>();
        foreach (int cell in cells)
        {
            result[cell] = new List<ChoreStatusInfo>();
        }

        if (GlobalChoreProvider.Instance == null)
        {
            return result;
        }

        // 遍历所有世界的任务列表
        foreach (var chore_list in GlobalChoreProvider.Instance.choreWorldMap.Values)
        {
            foreach (var chore in chore_list)
            {
                if (chore == null || chore.gameObject == null) continue;

                int chore_cell = Grid.PosToCell(chore.gameObject);
                if (cells.Contains(chore_cell))
                {
                    var chore_info = new ChoreStatusInfo
                    {
                        ChoreType = chore.choreType.Id.ToString(),
                        Priority = chore.masterPriority.priority_value,
                        PriorityClass = chore.masterPriority.priority_class.ToString()
                    };

                    if (chore.isComplete)
                    {
                        chore_info.Status = "Completed";
                    }
                    else if (chore.driver != null)
                    {
                        chore_info.Status = "In Progress";
                        chore_info.Assignee = chore.driver.GetComponent<KPrefabID>().GetProperName();
                    }
                    else
                    {
                        chore_info.Status = "Pending";
                    }

                    // 检查可达性
                    chore_info.IsReachable = IsChoreReachable(chore);

                    result[chore_cell].Add(chore_info);
                }
            }
        }

        return result;
    }

    private static bool IsChoreReachable(Chore chore)
    {
        foreach (var precondition in chore.GetPreconditions())
        {
            if (precondition.condition.id == "IsReachable")
            {
                // 创建一个临时的上下文来测试可达性
                // 注意：这可能不是100%准确，因为它没有一个真实的消费者状态
                var context = new Chore.Precondition.Context(chore, new ChoreConsumerState(null), false);
                return precondition.condition.fn(ref context, precondition.data);
            }
        }
        // 如果没有可达性先决条件，我们假设它是可达的
        return true;
    }
}
