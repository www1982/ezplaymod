# EZPlay - 外部控制与自动化框架

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

#### `.gitignore`

- **文件功能**: Git 版本控制的忽略文件，用于排除编译产物（如 `bin/` 和 `obj/` 目录）、用户特定的 IDE 配置文件（如 `.vs/`, `*.user`）以及其他临时文件。
- **实现逻辑**: 遵循标准的 `.gitignore` 格式，确保代码仓库只包含核心的源代码和项目文件，保持整洁并避免不必要的冲突。

#### `1.spec`

- **文件功能**: PyInstaller 配置文件，用于将 Python 测试脚本（`projects/modtest/1.py`）打包成一个独立的可执行文件。
- **实现逻辑**: 定义了打包过程中的各项参数，如入口脚本、数据文件、依赖项等。这表明项目可能使用 Python 进行一些自动化测试或辅助工具的开发。

#### `config.json`

- **文件功能**: Mod 的核心配置文件，允许用户自定义网络端口。
- **实现逻辑**: 在 Mod 启动时由 `ModLoader` 读取，用于设置 API 服务器和事件服务器的监听端口，避免了硬编码带来的端口冲突问题。

#### `whitelist.json`

- **文件功能**: 安全沙箱机制的核心配置文件，用于精确控制哪些游戏代码可以通过反射 API 暴露给外部程序。
- **实现逻辑**: 文件采用 JSON 格式，键是类的完全限定名，值是允许调用的公共方法或属性名数组。`"*": ["*"]` 的配置会禁用安全检查，允许所有反射调用。

#### `CODE_REVIEW.md`

- **文件功能**: 一份详细的代码审查报告，指出了项目中的严重问题、潜在风险和优化建议。是理解项目当前状态和未来开发方向的关键文档。

#### `bin/`, `obj/`, `lib/` 目录

- **`bin/`**: 存放编译器生成的最终程序集（DLL 文件）。Debug 和 Release 两种配置会产生不同版本的输出，通常此目录会被 `.gitignore` 排除。
- **`obj/`**: 存放编译过程中产生的临时文件和中间文件。此目录对于最终的 Mod 运行不是必需的，同样被 `.gitignore` 排除。
- **`lib/`**: （如果存在）通常用于存放项目依赖的第三方库文件（DLLs），例如 `0Harmony.dll` 等。这些库文件是项目成功编译和运行的前提。

#### 其他系统文件

- **.DS_Store**: macOS 系统生成的隐藏文件，用于存储文件夹的自定义属性。通常可以忽略，不影响 Mod 功能。
- **.git/**: Git 版本控制仓库目录，包含项目的变更历史。如果您不是开发者，可以忽略此目录。

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
- **关键逻辑**: 处理蓝图的整体生命周期，包括从扫描到放置的端到端流程。通过调用 `BlueprintScanner` 和 `BlueprintPlacer` 来实现具体操作，并管理蓝图的序列化和反序列化。

#### `BlueprintScanner.cs`

- **文件功能**: 负责将游戏内的指定区域扫描并转换为蓝图数据。
- **实现逻辑**: 遍历指定区域内的所有格子和预设的图层（建筑、管道、电线等），从 `GameObject` 中提取建筑 ID、相对位置、元素、方向等信息，并填充到 `Blueprint` 对象中。支持多种建筑类型，并处理特殊情况如旋转和元素覆盖。

#### `BlueprintPlacer.cs`

- **文件功能**: 实现了蓝图的异步、分阶段放置逻辑。
- **实现逻辑**: 作为一个复杂的状态机，它按正确的顺序（挖掘 -> 建造 -> 管道 -> 电线）下达游戏指令。通过监听 `ChoreCreationPatch` 捕获的任务创建事件，来跟踪每个阶段的建造进度，并在当前阶段所有任务完成后自动进入下一阶段。使用模板系统（如 `TemplateCache`）来高效生成建造命令。

#### `Models.cs`

- **文件功能**: 定义了蓝图系统的数据结构。
- **关键类**:
  - `Blueprint`: 蓝图的顶层容器，包含名称、尺寸以及分类的建筑/瓦片列表。
  - `BlueprintItem`: 代表蓝图中的一个独立对象，包含预设 ID、偏移量、元素、方向和设置。

---

### `GameState/` - 游戏状态管理

该模块是 Mod 的核心数据源，负责捕获、缓存和组织游戏世界的完整状态，为外部 API 提供了全面的数据查询基础。

#### `GameStateManager.cs`

- **文件功能**: 作为状态管理的中央协调器，定期生成整个殖民地的状态快照，并将其缓存以供 API 查询和事件广播。
- **实现逻辑**:
  - `UpdateState()`: 核心的数据采集方法。它会遍历游戏内的所有世界（星球），聚合关键信息，如各类资源的总量、所有复制人的详细状态、当前科研项目的进度以及游戏内的每日报告等，最终将这些信息整合到一个顶层的 `ColonyState` 对象中。
  - `Tick()`: 该方法由 `GameStateMonitorPatch` 补丁在游戏每一帧调用，但为了防止对游戏性能造成冲击，其内部实现了一个节流逻辑（计时器），确保实际的状态更新与广播操作大约每 5 秒才执行一次。这种设计在保证数据相对实时性的同时，极大地降低了性能开销。

#### `Models.cs`

- **文件功能**: 定义了所有用于描述游戏状态的数据结构（Data Transfer Objects, DTOs）。这些模型是 API 通信的基石，确保了内部状态数据能够被清晰、一致地序列化为 JSON 格式，并被外部应用程序理解。
- **关键类**:
  - `ColonyState`: 描述整个殖民地状态的顶层容器，包含了所有世界的信息和全局数据。
  - `WorldState`: 封装了单个星球（世界）的详细状态，如资源、建筑、生物等。
  - `GameEvent`: 定义了通过 `EventSocketServer` 广播的所有事件的标准化结构，确保了事件数据的一致性。

---

### `Logistics/` - 后勤系统

#### `LogisticsManager.cs`

- **文件功能**: 负责注册、注销和执行由外部 API 定义的后勤策略。
- **关键逻辑**: 维护一个策略列表 `_policies`，通过 `RegisterPolicy` 和 `UnregisterPolicy` 方法管理策略。`Tick` 方法每 10 秒（通过计时器控制）执行一次，遍历所有策略并调用其 `Execute` 方法。目前仅支持一种策略类型（`PolicyType.DuplicantChore`），但设计为可扩展。
- **实现逻辑**: 使用 `ServiceContainer` 解析 `ILogger` 和 `IEventBroadcaster` 服务，确保日志记录和事件广播的解耦。策略执行时，会根据策略类型分派到相应的处理器（如 `HandleDuplicantChorePolicy`），但当前仅记录日志，实际逻辑待实现。

#### `Models.cs`

- **文件功能**: 定义了后勤系统的数据结构，用于描述外部传入的后勤策略。
- **关键类**:
  - `LogisticsPolicy`: 策略的基类，包含 ID、类型（`PolicyType` 枚举）和负载（泛型数据）。
  - `PolicyType`: 枚举定义了可能的策略类型，目前仅 `DuplicantChore`（复制人任务相关）。
  - 实现逻辑: 使用 Newtonsoft.Json 进行序列化，支持灵活的策略扩展。

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

- **`BuildingStatusPatches.cs`**: 包含多个补丁，用于监听建筑的各种状态变化。

  - **功能**:
    - `Repairable_InitializeStates_Patch`: 在建筑损坏时广播 `Alert.Building.Broken` 事件。
    - `BuildingDeconstructedPatch`: 在建筑被拆除时广播 `Milestone.Building.Deconstructed` 事件。
    - `StorageStorePatch` / `StorageRemovePatch`: 在存储器内容发生变化（存入或取出物品）时广播 `StateChange.Storage.ContentChanged` 事件。
    - `BuildingOverheatingPatch`: 在建筑过热时广播 `Alert.Building.Overheated` 事件。
  - **实现细节**: 通过 `Postfix` 补丁注入到 `Repairable`, `Deconstructable`, `Storage`, `Overheatable` 等组件的关键方法中，捕获事件并提取相关数据（如建筑 ID、位置、物品信息）进行广播。

- **`ChoreCreationPatch.cs`**: 监听全局任务的创建。

  - **功能**: 捕获游戏中所有新创建的任务（Chore），并通知 `BlueprintPlacer`。
  - **实现细节**: 对 `GlobalChoreProvider.AddChore` 方法应用 `Postfix` 补丁。这是蓝图系统自动建造的关键环节，通过它 `BlueprintPlacer` 可以跟踪每个建造任务是否已生成，从而判断建造进度。

- **`DispatcherPatch.cs`**: 驱动主线程任务调度和周期性事件。

  - **功能**:
    1.  在游戏主循环的每一帧结束时，调用 `MainThreadDispatcher.ProcessQueue()` 来执行所有排队的跨线程任务。
    2.  每秒广播一次 `Simulation.Tick` 事件，包含当前游戏时间、周期和暂停状态。
  - **实现细节**: 对 `Game.Update` 方法应用 `Postfix` 补丁，确保了与游戏主逻辑的同步。

- **`DuplicantEventPatch.cs`**: 监听复制人死亡事件。

  - **功能**: 在复制人死亡时广播 `DuplicantDeath` 事件。
  - **实现细节**: 对 `MinionIdentity.OnDied` 方法应用 `Postfix` 补丁，提供了一个简单的死亡通知。

- **`DuplicantLifecyclePatches.cs`**: 监听复制人生命周期中的多个关键事件。

  - **功能**:
    - `DuplicantPrintedPatch`: 复制人被打印（生成）时。
    - `DuplicantDeathPatch`: 复制人死亡时（提供了比 `DuplicantEventPatch` 更详细的信息，如死因）。
    - `DuplicantGainedSkillPatch`: 复制人学会新技能时。
    - `DuplicantStressBreakPatch`: 复制人压力崩溃时。
    - `DuplicantDiseaseGainedPatch`: 复制人感染疾病时。
    - `DuplicantAttributeChangedPatch`: 复制人属性（如力量、智力）发生变化时。
  - **实现细节**: 对 `Telepad`, `DeathMonitor.Instance`, `MinionResume`, `StressMonitor.Instance`, `Sicknesses`, `AttributeInstance` 等多个类的方法进行补丁，全面覆盖了复制人的成长和状态变化。

- **`GameLoadPatch.cs`**: 在加载游戏存档时执行清理操作。

  - **功能**: 清理 `GameObjectManager` 的缓存，防止因游戏对象 ID 重用而导致的引用错误。
  - **实现细节**: 对 `Game.Load` 方法应用 `Postfix` 补丁，确保每次加载存档时缓存都是干净的。

- **`GameStateMonitorPatch.cs`**: 驱动 `GameStateManager` 的定期更新。

  - **功能**: 在游戏每一帧调用 `GameStateManager.Tick()`，由其内部的节流逻辑控制状态快照的生成和广播。
  - **实现细节**: 对 `Game.Update` 方法应用 `Postfix` 补丁，是整个游戏状态监控的数据来源。

- **`MilestoneEventsPatch.cs`**: 监听里程碑事件，如打印舱出现新选项。

  - **功能**: 当打印舱有新的可打印项目（复制人或物品）时，广播 `Milestone.NewPrintablesAvailable` 事件。
  - **实现细节**: 通过对 `Immigration.Sim200ms` 应用 `Prefix` 和 `Postfix` 补丁，比较方法执行前后的 `ImmigrantsAvailable` 状态来精确判断新选项出现的时刻。

- **`ResearchEventPatch.cs`**: 监听研究完成事件。

  - **功能**: 在一项科技研究完成时，广播 `ResearchComplete` 事件。
  - **实现细节**: 对 `Research.CompleteResearch` 方法应用 `Postfix` 补丁。

- **`StateChangeEventsPatch.cs`**: 监听间歇泉状态变化。

  - **功能**: 在间歇泉的状态（如休眠、活跃、喷发）发生改变时，广播 `StateChange.GeyserStateChanged` 事件。
  - **实现细节**: 对 `Geyser.OnStateChanged` 方法应用 `Postfix` 补丁。

- **`WorldEventsPatches.cs`**: 监听世界事件，如间歇泉喷发和流星雨。
  - **功能**:
    - `GeyserStartEruptingPatch` / `GeyserStopEruptingPatch`: 间歇泉开始或停止喷发时。
    - `NewElementDiscoveredPatch`: 首次发现新元素时。
    - `MeteorShowerPatch`: 流星雨开始时。
  - **实现细节**: 对 `Geyser`, `DiscoveredResources`, `MeteorShowerEvent.States` 等相关类的方法进行补丁，捕获影响整个游戏世界的关键动态。

## 4. 代码审查摘要与未来方向 (Code Review Summary & Future Work)
