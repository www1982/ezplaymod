# EZPlay - 外部控制与自动化框架

> [!WARNING] > **项目状态**: 本项目的所有 Harmony 补丁当前均被注释，导致 Mod 核心功能无效。在手动激活补丁前，请将其视为一个代码学习示例。详情请见文末的“代码审查摘要”。

## 1. Mod 简介与核心功能 (Introduction & Features)

- **EZPlay** 是一个为《缺氧》设计的强大 Mod 框架，它通过 WebSocket 建立了一个外部 API 服务器，允许第三方程序实时监控、查询和控制游戏状态。其核心功能包括：
  - **实时数据流**: 通过 WebSocket 广播详细的游戏状态更新，包括资源储量、复制人状态、科研进度、世界事件等。
  - **远程指令执行**: 接收外部指令，执行复杂的游戏内操作，如设置复制人工作优先级、管理科研队列、创建和执行蓝图、控制游戏速度等。
  - **事件驱动架构**: 监听并广播大量的游戏内事件，例如建筑损坏、复制人死亡、技能获得、间歇泉喷发等，为自动化和外部工具提供了丰富的触发器。
  - **蓝图系统**: 提供了扫描、保存、加载和部署建筑蓝图的功能，极大地简化了复杂基地的建造流程。
  - **安全沙箱**: 通过白名单机制限制高风险的反射操作，确保 API 的稳定性和安全性。

## 2. 安装与使用 (Installation & Usage)

- **安装**:
  1.  从 Mod 发布页下载最新的`EZPlay` Mod。
  2.  将 Mod 文件夹解压并放置到《缺氧》的本地 Mod 目录中。通常位于 `~/Documents/Klei/OxygenNotIncluded/mods/local/`。
  3.  启动游戏，在主菜单的 "Mods" 选项中，找到并启用 "EZPlay" Mod。
  4.  重启游戏使 Mod 生效。
- **使用**:
  - Mod 启动后会自动根据 `config.json` 文件中的配置启动 API 服务和事件广播服务（默认为 `ws://127.0.0.1:8080/api` 和 `ws://127.0.0.1:8081/`）。
  - 开发者可以通过任何支持 WebSocket 的编程语言连接到这些端点，以接收游戏数据并发送控制指令。
  - 所有 API 请求和响应都使用 JSON 格式。具体的 API 端点和数据结构请参考技术实现部分的详细解析。
  - 出于安全考虑，API 服务器默认只接受来自本机 (`127.0.0.1`) 的连接。

## 3. 技术实现深度解析 (In-depth Technical Breakdown)

### 根目录文件

#### `EZPlay.csproj`

- **文件功能**: .NET 项目文件，定义了 Mod 的构建配置、目标框架（`.NET Framework 4.7.2`）以及所有依赖项，如游戏核心程序集、Unity 引擎库和第三方库（Harmony, Newtonsoft.Json, WebSocketSharp）。

#### `config.json`

- **文件功能**: Mod 的核心配置文件，允许用户自定义网络端口。
- **实现逻辑**: 在 Mod 启动时由 `ModLoader` 读取，用于设置 API 服务器和事件服务器的监听端口，避免了硬编码带来的端口冲突问题。

#### `whitelist.json`

- **文件功能**: 安全沙箱机制的核心配置文件，用于精确控制哪些游戏代码可以通过反射 API 暴露给外部程序。
- **实现逻辑**: 文件采用 JSON 格式，键是类的完全限定名，值是允许调用的公共方法或属性名数组。`"*": ["*"]` 的配置会禁用安全检查，允许所有反射调用。

#### `CODE_REVIEW.md`

- **文件功能**: 一份详细的代码审查报告，指出了项目中的严重问题、潜在风险和优化建议。是理解项目当前状态和未来开发方向的关键文档。

---

### `Core/` - 核心框架

#### `ModLoader.cs`

- **文件功能**: Mod 的入口点，继承自 `UserMod2`。
- **关键逻辑**:
  - `OnLoad()`: 在 Mod 加载时执行初始化，包括：
    1.  创建并注册 `Logger` 和 `SecurityWhitelist` 等核心服务到 `ServiceContainer`。
    2.  读取 `config.json` 获取端口配置。
    3.  启动 `ApiServer` 和 `EventSocketServer`。
    4.  将 `LogisticsManager.Tick` 等需要定期执行的方法注册到主线程调度器。

#### `ServiceContainer.cs`

- **文件功能**: 实现了一个简单的静态服务定位器（Service Locator），用于在整个 Mod 范围内注册和解析服务实例。
- **实现逻辑**: 使用一个静态字典 `_services` 来存储服务实例，通过 `Register<T>()` 和 `Resolve<T>()` 方法进行服务的存取，实现了依赖解耦。

#### `Logger.cs` & `Interfaces/ILogger.cs`

- **文件功能**: 提供了一个带日志级别（DEBUG, INFO, WARNING, ERROR）和分类前缀的日志记录工具。
- **实现逻辑**: 封装了 `UnityEngine.Debug` 的方法，允许根据 `CurrentLogLevel` 过滤日志输出。

#### `SecurityWhitelist.cs` & `Interfaces/ISecurityWhitelist.cs`

- **文件功能**: 实现了安全白名单机制，用于控制反射 API 的访问权限和 WebSocket 的 IP 访问权限。
- **实现逻辑**: 在构造时读取 `whitelist.json` 文件，将规则解析到内存中。提供 `IsAllowed(typeName, memberName)` 和 `IsIPAllowed(ipAddress)` 方法供 `ReflectionExecutor` 和 `ApiServer` 调用。

#### `Interfaces/IEventBroadcaster.cs`

- **文件功能**: 定义了事件广播器的标准接口，确保了事件广播功能的可替换性和统一性。

---

### `API/` - 外部接口

#### `ApiServer.cs` & `EventSocketServer.cs`

- **文件功能**: 分别实现了两个独立的 WebSocket 服务器。`ApiServer` 用于接收指令并返回结果，`EventSocketServer` 用于向所有客户端广播游戏事件。
- **关键逻辑**:
  - `ApiServer`: 在后台线程中运行，接收到消息后，使用 `MainThreadDispatcher` 将请求处理逻辑（`RequestHandler.HandleRequest`）调度到游戏主线程执行，确保线程安全。
  - `EventSocketServer`: 实现了 `IEventBroadcaster` 接口，提供 `BroadcastEvent` 方法，将事件序列化为 JSON 并广播给所有连接的客户端。

#### `RequestHandler.cs`

- **文件功能**: API 请求的中央路由器。
- **实现逻辑**: `HandleRequest` 方法根据请求中的 `action` 字段，通过 `switch` 语句将请求分派给相应的 `Executor`（执行器）或 `Query`（查询器）进行处理，并统一封装返回结果。

#### `Models.cs`

- **文件功能**: 定义了 API 通信的核心数据结构。
- **关键类**:
  - `ApiRequest`: 封装了从客户端接收的请求（action, payload, requestId）。
  - `ApiResponse`: 封装了发送到客户端的响应（type, status, payload, requestId）。
  - `ExecutionResult`: 用于在 Executor/Query 和 RequestHandler 之间传递内部执行结果。

#### `Exceptions/ApiException.cs`

- **文件功能**: 定义了自定义异常 `ApiException`，用于在 API 处理流程中抛出包含状态码和详细信息的业务逻辑错误。

#### `Executors/` - 指令执行器

- **`BlueprintExecutor.cs`**: 处理蓝图扫描和创建的请求。
- **`BuildingDestroyer.cs`**: 处理拆除建筑的请求。
- **`GlobalActionExecutor.cs`**: 处理全局操作，如截图、暂停/继续游戏。
- **`LogisticsExecutor.cs`**: 处理后勤策略的注册与注销。
- **`PersonnelExecutor.cs`**: 一个庞大的类，负责处理所有与“人”相关的复杂操作，包括：
  - **复制人**: 设置工作优先级、学习技能、管理允许的消耗品。
  - **日程**: 创建新日程、修改日程区块、为复制人分配日程。
  - **打印舱**: 获取可打印选项、选择打印项目。
  - **科研**: 设置研究队列、取消当前研究。
- **`ReflectionExecutor.cs`**: 执行通过白名单验证的反射调用，允许外部程序直接操作游戏对象的组件属性或方法。

#### `Queries/` - 数据查询器

- **`ChoreStatusQueryExecutor.cs`**: 查询指定复制人当前的任务状态。
- **`FindObjectsQueryExecutor.cs`**: 根据组件名称在游戏中查找所有匹配的游戏对象。
- **`GridQueryExecutor.cs`**: 查询指定坐标格子的详细信息（元素、温度、质量、建筑）。
- **`PathfindingQueryExecutor.cs`**: 提供一个简化的寻路查询，判断两个点是否在同一个房间内。

---

### `Blueprints/` - 蓝图系统

#### `BlueprintManager.cs`

- **文件功能**: 蓝图系统的入口和管理器，负责协调蓝图的创建、保存、加载和放置。

#### `BlueprintScanner.cs`

- **文件功能**: 负责将游戏内的指定区域扫描并转换为蓝图数据。
- **实现逻辑**: 遍历指定区域内的所有格子和预设的图层（建筑、管道、电线等），从 `GameObject` 中提取建筑 ID、相对位置、元素、方向等信息，并填充到 `Blueprint` 对象中。

#### `BlueprintPlacer.cs`

- **文件功能**: 实现了蓝图的异步、分阶段放置逻辑。
- **实现逻辑**: 作为一个复杂的状态机，它按正确的顺序（挖掘 -> 建造 -> 管道 -> 电线）下达游戏指令。通过监听 `ChoreCreationPatch` 捕获的任务创建事件，来跟踪每个阶段的建造进度，并在当前阶段所有任务完成后自动进入下一阶段。

#### `Models.cs`

- **文件功能**: 定义了蓝图系统的数据结构。
- **关键类**:
  - `Blueprint`: 蓝图的顶层容器，包含名称、尺寸以及分类的建筑/瓦片列表。
  - `BlueprintItem`: 代表蓝图中的一个独立对象，包含预设 ID、偏移量、元素、方向和设置。

---

### `GameState/` - 游戏状态管理

#### `GameStateManager.cs`

- **文件功能**: 负责定期创建整个殖民地的状态快照，并将其缓存以供 API 查询和广播。
- **实现逻辑**:
  - `UpdateState()`: 遍历所有星球，收集资源总量、复制人状态、科研进度、每日报告等详细信息，并组织到 `ColonyState` 对象中。
  - `Tick()`: 由 `GameStateMonitorPatch` 每帧调用，但内部使用计时器确保状态更新和广播大约每 5 秒执行一次，以避免性能问题。

#### `Models.cs`

- **文件功能**: 定义了用于捕获和序列化游戏状态的各种数据模型。
- **关键类**:
  - `ColonyState`: 顶层状态容器。
  - `WorldState`: 单个星球的状态。
  - `GameEvent`: 通过 `EventSocketServer` 广播的所有事件的标准格式。

---

### `Logistics/` - 后勤系统

#### `LogisticsManager.cs`

- **文件功能**: 负责注册、注销和执行由外部 API 定义的后勤策略。
- **实现逻辑**: 包含一个 `Tick` 方法，以 10 秒为周期被调用，用于执行已注册的策略。目前功能尚在开发中。

#### `Models.cs`

- **文件功能**: 定义了后勤系统的数据结构，主要是 `LogisticsPolicy` 类和 `PolicyType` 枚举。

---

### `Utils/` - 工具类

#### `MainThreadDispatcher.cs`

- **文件功能**: 提供了一个关键的工具，用于将代码从任何线程（特别是 WebSocket 服务器的后台线程）安全地调度到游戏的主线程上执行。
- **实现逻辑**: 使用一个线程安全的队列 `ExecutionQueue` 存放待执行的 `Action`。`RunOnMainThread` 方法将任务入队并返回一个 `Task`，而 `ProcessQueue` 方法（由 `DispatcherPatch` 在主线程的 `Update` 循环中调用）则负责从队列中取出并执行任务。

#### `GameObjectManager.cs`

- **文件功能**: 提供了一个游戏对象缓存，用于在需要时通过唯一 ID 安全地引用游戏对象。
- **实现逻辑**: 使用 `WeakReference<GameObject>` 作为缓存值，这可以防止缓存本身阻止游戏对象被垃圾回收，从而避免了内存泄漏。

#### `ImmigrationHelper.cs`

- **文件功能**: 使用 C# 反射来访问 `Immigration` 类的私有成员。
- **实现逻辑**: 封装了对打印舱内部方法的调用，允许获取所有可用的打印选项、选择或拒绝它们，提供了比游戏原生 API 更强大的控制能力。

---

### `Patches/` - Harmony 补丁

此目录下的所有文件都使用 `Harmony` 库来“修补”游戏的原生代码，以便在特定事件发生时插入我们自己的逻辑（通常是广播一个 WebSocket 事件）。

- **`BuildingStatusPatches.cs`**: 监听建筑状态变化，如损坏、拆除、过热和存储内容变化。
- **`ChoreCreationPatch.cs`**: 监听全局任务的创建，主要用于 `BlueprintPlacer` 跟踪建造进度。
- **`DispatcherPatch.cs`**: 在游戏主循环的每一帧结束时，调用 `MainThreadDispatcher.ProcessQueue()` 来执行所有排队的跨线程任务。
- **`DuplicantEventPatch.cs`**: 监听复制人死亡事件。
- **`DuplicantLifecyclePatches.cs`**: 监听复制人生命周期事件，如打印、获得技能、压力崩溃等。
- **`GameLoadPatch.cs`**: 在加载游戏存档时，清理 `GameObjectManager` 的缓存。
- **`GameStateMonitorPatch.cs`**: 驱动 `GameStateManager` 的定期更新。
- **`MilestoneEventsPatch.cs`**: 监听里程碑事件，如打印舱出现新选项。
- **`ResearchEventPatch.cs`**: 监听研究完成事件。
- **`StateChangeEventsPatch.cs`**: 监听间歇泉状态变化。
- **`WorldEventsPatches.cs`**: 监听世界事件，如间歇泉喷发和流星雨。

## 4. 代码审查摘要与未来方向 (Code Review Summary & Future Work)

根据 `CODE_REVIEW.md` 的详细分析，本项目拥有巨大的潜力，但也存在一些待解决的问题和可以优化的方向。以下列表可作为社区贡献的路线图。

| ID          | 分类     | 问题描述                                  | 核心建议                                                                                            |
| :---------- | :------- | :---------------------------------------- | :-------------------------------------------------------------------------------------------------- |
| **SEV-01**  | **严重** | **Harmony 补丁未激活**                    | **移除所有 `[HarmonyPatch]` 属性前的 `//` 注释，使 Mod 生效。**                                     |
| **RISK-01** | **风险** | 每帧都进行 WebSocket 广播，导致性能问题   | 将高频事件广播（如 `Simulation.Tick`）移至低频更新循环（如 `Sim200ms`），并实现增量更新或节流。     |
| **RISK-02** | **风险** | 安全机制不明确且冗余（硬编码 + 文件配置） | 统一使用 `whitelist.json` 文件进行 IP 校验，移除代码中的硬编码列表。                                |
| **RISK-03** | **风险** | 建筑损坏事件的注入方式不健壮且冗余        | 监听 `Repairable.States` 的 `allowed` 状态的 `Enter` 事件，而非修补 `OnSpawn`。                     |
| **OPT-01**  | **优化** | API 和事件服务器的端口号被硬编码          | 将端口号移至外部 `config.json` 文件，允许用户自定义。                                               |
| **OPT-02**  | **优化** | 识别新打印复制人的逻辑存在竞态条件风险    | 组合使用 `Prefix` 和 `Postfix` 补丁修补 `Telepad.OnAcceptDelivery`，以更精确地关联新生成的复制人。  |
| **OPT-03**  | **优化** | 复制人压力崩溃类型被硬编码 (`BingeEat`)   | 通过 `StressMonitor.Instance.GetCurrentReactable()` 获取真实的压力反应类型。                        |
| **OPT-06**  | **优化** | 存储事件的捕获方式低效且脆弱              | 移除对 `Storage.Store/Remove` 的补丁，改用原生的 `OnStorageChange` 事件，并实现事件聚合与延迟广播。 |
