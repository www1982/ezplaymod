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
  - Mod 启动后会自动在 `ws://127.0.0.1:8080/api` 启动主 API 服务，并在 `ws://127.0.0.1:8081/` 启动事件广播服务。
  - 开发者可以通过任何支持 WebSocket 的编程语言连接到这些端点，以接收游戏数据并发送控制指令。
  - 所有 API 请求和响应都使用 JSON 格式。具体的 API 端点和数据结构请参考技术实现部分的详细解析。
  - 出于安全考虑，API 服务器默认只接受来自本机 (`127.0.0.1`) 的连接。

## 3. 技术实现深度解析 (In-depth Technical Breakdown)

### API/ApiServer.cs

- **文件功能概述**: 该文件负责创建和管理核心的 WebSocket API 服务器。它处理客户端连接、消息收发，并将请求分派到主线程进行处理。
- **关键类与方法详解**:
  - **`ApiServer`**:
    - **`Start()`**:
      - **Hook 类型**: 自定义新方法。
      - **目标**: 初始化并启动一个监听 `ws://0.0.0.0:8080` 的 WebSocket 服务器。
      - **实现逻辑**: 在一个后台线程中创建并启动 `WebSocketServer` 实例，避免阻塞游戏主线程。它还定义了一个简单的 IP 白名单，只允许本地连接。
    - **`Stop()`**:
      - **Hook 类型**: 自定义新方法。
      - **目标**: 安全地停止 WebSocket 服务器并中止其线程。
      - **实现逻辑**: 调用 `WebSocketServer.Stop()` 并中止相关线程。
  - **`GameService`**:
    - **`OnMessage(MessageEventArgs e)`**:
      - **Hook 类型**: `WebSocketBehavior` 的重写方法。
      - **目标**: 处理从客户端收到的每一个 WebSocket 消息。
      - **实现逻辑**:
        1.  验证客户端 IP 是否在白名单内。
        2.  将收到的 JSON 字符串反序列化为 `ApiRequest` 对象。
        3.  使用 `MainThreadDispatcher` 将请求处理逻辑（`RequestHandler.HandleRequest`）调度到游戏主线程执行，以确保线程安全。
        4.  通过 `Task.ContinueWith` 异步地等待主线程处理结果。
        5.  将执行结果（成功或异常）序列化为 JSON 并发送回客户端。

### API/EventSocketServer.cs

- **文件功能概述**: 实现了一个独立的 WebSocket 服务器，专门用于向所有连接的客户端广播实时的游戏事件。
- **关键类与方法详解**:
  - **`EventSocketServer`**:
    - **`BroadcastEvent(string eventType, object payload)`**:
      - **Hook 类型**: 自定义新方法，实现 `IEventBroadcaster` 接口。
      - **目标**: 将一个结构化的游戏事件广播给所有监听者。
      - **实现逻辑**: 将事件类型和数据封装在 `GameEvent` 对象中，序列化为 JSON，并通过 `WebSocketServer` 的广播功能发送出去。

### API/RequestHandler.cs

- **文件功能概述**: API 请求的中央路由器。它根据请求中的 `action` 字段，将请求分派给相应的`Executor`或`Query`类进行处理。
- **关键类与方法详解**:
  - **`RequestHandler`**:
    - **`HandleRequest(ApiRequest request)`**:
      - **Hook 类型**: 自定义新方法。
      - **目标**: 解析 API 请求并调用相应的业务逻辑。
      - **实现逻辑**:
        1.  从 `ApiRequest` 中提取 `action` 和 `payload`。
        2.  使用 `if-else if` 或 `switch` 语句，根据 `action` 的前缀或全名（如 `Duplicant.*`, `find_objects`）来决定调用哪个处理模块。
        3.  调用相应的静态方法（如 `PersonnelExecutor.HandleDuplicantAction`, `FindObjectsQueryExecutor.Execute`）并传递 `payload`。
        4.  将返回的结果包装成一个标准的 `ApiResponse` 对象。

### Blueprints/BlueprintManager.cs

- **文件功能概述**: 管理蓝图的创建、保存、加载和放置。
- **关键类与方法详解**:
  - **`BlueprintManager`**:
    - **`CreateBlueprint(...)`**:
      - **Hook 类型**: 自定义新方法。
      - **目标**: 根据给定的游戏对象列表创建一个蓝图对象。
      - **实现逻辑**: 遍历游戏对象，提取其预设 ID、相对位置、元素、方向和可白名单化的组件设置，并存入 `Blueprint` 对象。
    - **`PlaceBlueprint(Blueprint blueprint)`**:
      - **Hook 类型**: 自定义新方法。
      - **目标**: 在游戏世界中放置一个蓝图。
      - **实现逻辑**: 调用 `BlueprintPlacer.PlaceBlueprint` 来启动异步的放置流程。

### Blueprints/BlueprintPlacer.cs

- **文件功能概述**: 实现了蓝图的异步、分阶段放置逻辑。这是一个复杂的状态机，确保蓝图按正确的顺序（挖掘->建造->管道->电线）被执行。
- **关键类与方法详解**:
  - **`BlueprintPlacerInstance`**:
    - **`Tick()`**:
      - **Hook 类型**: `MonoBehaviour.Update` 的驱动逻辑。
      - **目标**: 驱动蓝图放置的状态机。
      - **实现逻辑**:
        1.  检查当前阶段（如 `Digging`）。
        2.  如果处于 `PendingOrders` 阶段，则调用相应的方法（如 `ExecuteDiggingPhase`）来下达游戏指令（如放置挖掘指令）。然后切换到 `Executing` 阶段。
        3.  如果处于 `Executing` 阶段，则监控由这些指令创建的 `Chore` 是否全部完成。
        4.  一旦所有 `Chore` 完成，就推进到下一个阶段（如 `Building`），并重复此过程。
    - **`HandleChoreCreated(Chore chore, int cell)`**:
      - **Hook 类型**: 事件处理器。
      - **目标**: 监听由 `ChoreCreation_Patch` 捕获的新建任务。
      - **实现逻辑**: 如果新创建的 `Chore` 位于当前蓝图施工区域内，就将其加入 `_trackedChores` 列表进行监控。

### Core/ModLoader.cs

- **文件功能概述**: Mod 的入口点。负责在 Mod 加载时进行初始化设置。
- **关键类与方法详解**:
  - **`ModLoader`**:
    - **`OnLoad(Harmony harmony)`**:
      - **Hook 类型**: `UserMod2` 的重写方法。
      - **目标**: 初始化整个 Mod 框架。
      - **实现逻辑**:
        1.  注册 `Logger` 和 `SecurityWhitelist` 到 `ServiceContainer`。
        2.  启动 `ApiServer` 和 `EventSocketServer`。
        3.  注册 `LogisticsManager.Tick` 到主线程调度器。

### GameState/GameStateManager.cs

- **文件功能概述**: 负责定期快照整个殖民地的状态，并将其缓存以供 API 查询和广播。
- **关键类与方法详解**:
  - **`GameStateManager`**:
    - **`UpdateState()`**:
      - **Hook 类型**: 自定义新方法。
      - **目标**: 收集关于游戏各个方面的详细数据。
      - **实现逻辑**: 遍历所有已发现的星球，收集资源总量、复制人列表及其状态（压力、健康）、科研进度、每日报告摘要等信息，并将其组织到 `ColonyState` 对象中。
    - **`Tick()`**:
      - **Hook 类型**: 自定义新方法，由 `GameStateMonitorPatch` 每帧调用。
      - **目标**: 控制状态更新和广播的频率。
      - **实现逻辑**: 使用一个计时器，确保 `UpdateState` 和 WebSocket 广播大约每 5 秒执行一次，以避免性能问题。

### Patches/

- **文件功能概述**: 此目录下的所有文件都使用 `Harmony` 库来“修补”游戏的原生代码，以便在特定事件发生时插入我们自己的逻辑（通常是广播一个 WebSocket 事件）。
- **关键类与方法详解**:
  - **`ChoreCreation_Patch`**:
    - **`Postfix(Chore chore)` on `GlobalChoreProvider.AddChore`**:
      - **Hook 类型**: `Postfix`。
      - **目标**: `GlobalChoreProvider.AddChore`。
      - **实现逻辑**: 每当游戏创建一个新的全局任务（Chore）时，此补丁会捕获该任务，并通知 `BlueprintPlacer`，用于蓝图建造的进度跟踪。
  - **`DispatcherPatch`**:
    - **`Postfix()` on `Game.Update`**:
      - **Hook 类型**: `Postfix`。
      - **目标**: `Game.Update`。
      - **实现逻辑**: 在游戏主循环的每一帧结束时，调用 `MainThreadDispatcher.ProcessQueue()` 来执行所有排队的跨线程任务。
  - **`DuplicantLifecyclePatches.cs`**:
    - **`Postfix(...)` on `DeathMonitor.Instance.Kill`**:
      - **Hook 类型**: `Postfix`。
      - **目标**: `DeathMonitor.Instance.Kill`。
      - **实现逻辑**: 当一个复制人死亡时，广播一个 `Lifecycle.Duplicant.Death` 事件，包含死因等信息。
  - **`MilestoneEventsPatch.cs`**:
    - **`Postfix(Tech tech)` on `Research.CheckBuyResearch`**:
      - **Hook 类型**: `Postfix`。
      - **目标**: `Research.CheckBuyResearch`。
      - **实现逻辑**: 当一项研究完成时，广播一个 `ResearchComplete` 事件。

### Utils/MainThreadDispatcher.cs

- **文件功能概述**: 提供一个关键的工具，用于将代码从任何线程（特别是 WebSocket 服务器的后台线程）安全地调度到游戏的主线程上执行。
- **关键类与方法详解**:
  - **`MainThreadDispatcher`**:
    - **`RunOnMainThread<T>(Func<T> func)`**:
      - **Hook 类型**: 自定义新方法。
      - **目标**: 异步地在主线程上执行一个函数并返回结果。
      - **实现逻辑**: 将要执行的函数 `func` 包装在一个 `Action` 中，并放入一个线程安全的队列 `ExecutionQueue`。它返回一个 `Task`，当主线程处理完该 `Action` 后，这个 `Task` 就会完成并携带返回值。
    - **`ProcessQueue()`**:
      - **Hook 类型**: 自定义新方法。
      - **目标**: 执行队列中的所有待处理任务。
      - **实现逻辑**: 在主线程的 `Update` 循环中被调用，它会从队列中取出所有 `Action` 并依次执行。

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
| **OPT-04**  | **优化** | API 中通过名字查找复制人的方式不够健壮    | API 应强制使用 `KPrefabID.InstanceID` 作为操作复制人的唯一标识符。                                  |
| **OPT-05**  | **优化** | 拆除建筑时，无法获取回收的资源列表        | 使用 `Prefix` 补丁在 `Deconstructable.OnCompleteWork` 执行前记录材料，在 `Postfix` 中发送。         |
| **OPT-06**  | **优化** | 存储事件的捕获方式低效且脆弱              | 移除对 `Storage.Store/Remove` 的补丁，改用原生的 `OnStorageChange` 事件，并实现事件聚合与延迟广播。 |

**潜在问题与 Bug**:

- **硬编码与反射**: 在 `DuplicantPrintedPatch` 中，通过反射字符串 `"selectedDeliverables"` 来获取私有字段，这种做法比较脆弱。如果游戏更新修改了这个字段名，Mod 就会崩溃。可以考虑寻找更稳定的 Hook 点或使用更健壮的反射库。
- **线程安全**: `ApiServer` 在一个新线程中启动，虽然请求处理被调度回了主线程，但服务器的启动和停止逻辑本身（修改 `_server` 和 `_serverThread` 静态变量）没有使用锁，在高并发场景下理论上存在竞态条件风险（尽管在 Mod 生命周期中实际发生概率极低）。
- **异常处理**: `RequestHandler` 中的 `catch (Exception ex)` 过于宽泛，它会捕获所有类型的异常，包括一些可能不应该被捕获的系统级异常。最好能更精确地捕获预期的 `ApiException` 和其他业务逻辑异常。
- **性能考量**:
  - **`GameStateManager.UpdateState()`**: 这个方法非常耗时，因为它遍历了大量的游戏对象和数据。虽然目前被限制为 5 秒一次，但在游戏后期，当对象数量巨大时，仍然可能导致明显的卡顿。可以考虑：
    1.  将数据收集工作分摊到多个帧上完成。
    2.  只更新自上次快照以来发生变化的数据，而不是每次都重新构建整个状态树。
  - **JSON 序列化**: 在 `GameStateMonitorPatch` 和 `DispatcherPatch` 中，每帧或每 5 秒都会进行 JSON 序列化和广播。对于大型数据结构，这会产生大量的 GC Allocations（垃圾回收分配），频繁触发 GC 可能导致游戏卡顿。可以考虑使用更高性能的二进制序列化协议（如 Protobuf）或实现一个 JSON 对象池来复用对象。
- **Mod 兼容性**:
  - **补丁冲突**: Mod 大量使用了 Harmony 补丁，这些补丁直接修改了游戏的核心方法。如果另一个 Mod 也修改了相同的方法，就可能产生冲突，导致其中一个或两个 Mod 失效，甚至游戏崩溃。建议在文档中明确列出所有被 Patch 的方法，并考虑添加配置选项来禁用某些可能冲突的补丁。
  - **游戏更新**: 由于紧密耦合了游戏内部实现（类名、方法名、字段名），任何大型的游戏更新都可能破坏此 Mod。需要持续的维护来跟进游戏版本的变化。
- **代码规范与可读性**:
  - **命名**: 整体命名清晰，但存在一些不一致，例如 `policy_id` (snake_case) 和 `ActionName` (PascalCase) 同时出现在 API 模型中。建议统一 API 的数据模型命名规范。
  - **注释**: 大部分 `[HarmonyPatch]` 属性都被注释掉了。这使得不熟悉代码的人很难理解哪个补丁是激活的。在最终发布时，应清理这些注释，只保留有效的代码。
  - **代码结构**: `PersonnelExecutor.cs` 文件非常庞大（超过 600 行），包含了对复制人、日程、打印仓和研究等多个完全不同领域的处理逻辑。建议将其拆分为更小的、职责更单一的类，如 `DuplicantExecutor`, `ScheduleExecutor`, `PrintingPodExecutor` 等，以提高可维护性。
