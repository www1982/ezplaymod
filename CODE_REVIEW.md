# 《缺氧》Mod 代码审查报告

## 1. 综合评估

此 Mod 项目设计宏大，试图通过外部 API 深度介入并控制游戏状态，这在《缺氧》Mod 中非常罕见且具有挑战性。代码结构上，项目采用了依赖注入（ServiceContainer）、主线程调度（MainThreadDispatcher）等良好的设计模式，显示出开发者具备一定的软件工程素养。

然而，项目当前处于**完全不可用**的状态。经过审查发现，**所有 Harmony 补丁均被注释，导致 Mod 的核心功能完全失效**。API 服务器虽然可以启动，但无法与游戏世界进行任何交互。因此，本报告的后续部分将**假设所有补丁均已激活**，以便对代码逻辑本身进行审查，为后续开发提供参考。

## 2. 严重问题 (Severe Issues)

- **问题描述**: 所有 Harmony 补丁均未激活。
- **风险分析**: 这是最严重的问题，导致 Mod 没有任何实际功能。所有通过 Harmony 注入游戏逻辑的代码都不会执行。API 请求虽然能被接收，但由于`DispatcherPatch`未激活，无法在游戏主线程中处理，导致整个 API 系统瘫痪。
- **相关代码**:

  ```csharp
  // MOd/Patches/目录下所有文件的 `[HarmonyPatch(...)]` 属性均被注释。
  // 示例: MOd/Patches/DispatcherPatch.cs

  //[HarmonyPatch(typeof(Game), "Update")]
  public class DispatcherPatch
  {
      // ...
  }

  // 示例: MOd/Patches/BuildingStatusPatches.cs
  //[HarmonyPatch(typeof(Repairable), "OnSpawn")]
  public static class BuildingBrokenPatch
  {
      // ...
  }
  ```

- **修改建议**: 移除所有 `[HarmonyPatch(...)]` 属性前的 `//` 注释，以激活所有补丁。开发者需要逐一审查并确认每个补丁的目标方法和签名是否与当前游戏版本（`/Users/user/Desktop/CODE/Assembly-CSharp`）匹配，然后才能安全地激活它们。

## 3. 潜在风险 (Potential Risks)

- **问题描述**: 在每一游戏帧（`Game.Update`）都进行 WebSocket 广播。
- **风险分析**: `DispatcherPatch`补丁的目标是`Game.Update`，这是一个高频调用的函数。在此函数中，每次都序列化游戏状态并进行网络广播，会带来巨大的性能开销。这会导致游戏帧率（FPS）显著下降，产生大量的 GC（垃圾回收）压力，并且在网络状况不佳时可能导致延迟累积。
- **相关代码**:

  ```csharp
  // MOd/Patches/DispatcherPatch.cs
  public class DispatcherPatch
  {
      public static void Postfix()
      {
          MainThreadDispatcher.ProcessQueue();

          EZPlay.Core.ServiceContainer.Resolve<EventSocketServer>().BroadcastEvent(
              "Simulation.Tick",
              new
              {
                  game_time_in_seconds = GameClock.Instance.GetTime(),
                  cycle = GameClock.Instance.GetCycle(),
                  is_paused = SpeedControlScreen.Instance.IsPaused
              }
          );
      }
  }
  ```

- **修改建议**:

  1.  **降低广播频率**: 不要在`Game.Update`中广播。应改为在频率较低的`Game.Sim200ms`或自定义的计时器中进行广播，例如每秒 1-5 次。
  2.  **增量更新**: 不要每次都发送完整的游戏状态。应实现一个状态差异比较系统，仅在状态发生变化时才广播变更的部分。
  3.  **按需广播**: 仅在有客户端连接时才启动广播逻辑。

- **问题描述**: 安全机制不明确且存在冗余。
- **风险分析**: `ApiServer.cs`中硬编码了一个 IP 白名单，而`ModLoader.cs`中又从文件加载了一个`SecurityWhitelist`服务。这两者并未关联使用。这可能导致：1. 安全策略不一致，开发者可能误以为文件配置生效，但实际上是硬编码的 IP 在起作用。2. 增加了维护成本和代码理解难度。
- **相关代码**:

  ```csharp
  // MOd/API/ApiServer.cs
  private static readonly List<IPAddress> AllowedIPs = new List<IPAddress>
  {
      IPAddress.Parse("127.0.0.1"),
      IPAddress.Loopback,
      IPAddress.IPv6Loopback
  };

  // MOd/Core/ModLoader.cs
  var whitelist = new SecurityWhitelist(logger, "Mods/EZPlay/whitelist.json");
  ServiceContainer.Register<ISecurityWhitelist>(whitelist);
  ```

- **修改建议**: 统一安全机制。移除`ApiServer.cs`中的硬编码 IP 列表，改为从`ServiceContainer`中获取`ISecurityWhitelist`服务实例，并使用其进行 IP 校验。这样可以使安全策略完全由`whitelist.json`文件驱动，更具灵活性和可维护性。

## 4. 优化建议 (Optimization Suggestions)

- **问题描述**: API 服务器和事件服务器的端口号被硬编码。
- **风险分析**: 硬编码的端口（8080 和 8081）可能与其他应用程序冲突，导致 Mod 无法正常启动。用户无法在不修改代码的情况下解决此问题。
- **相关代码**:

  ```csharp
  // MOd/Core/ModLoader.cs
  var eventServer = new EventSocketServer("ws://0.0.0.0:8081");

  // MOd/API/ApiServer.cs
  _server = new WebSocketServer("ws://0.0.0.0:8080");
  ```

- **修改建议**: 将端口号提取到配置文件中（例如，一个`config.json`文件）。在 Mod 加载时读取配置文件，如果文件不存在则使用默认值。这样用户就可以在需要时自行修改端口。

---

- **问题描述**: 打印新复制人的识别逻辑存在竞态条件风险。
- **风险分析**: `DuplicantPrintedPatch` 通过查找 `arrivalTime` 最晚的复制人来确定新打印的个体。在绝大多数情况下这是可行的，但如果游戏在同一帧内通过其他方式（如 Debug 命令）创建了另一个复制人，这个逻辑可能会错误地将事件关联到非打印台产出的复制人身上，导致后续 API 操作目标错误。
- **相关代码**:
  ```csharp
  // MOd/Patches/DuplicantLifecyclePatches.cs
  public static class DuplicantPrintedPatch
  {
      public static void Postfix(CharacterSelectionController __instance)
      {
          // ...
          // We'll find the most recently created minion and assume it's the one we just printed.
          var minion = Components.LiveMinionIdentities.Items.OrderByDescending(m => m.arrivalTime).FirstOrDefault();
          // ...
      }
  }
  ```
- **修改建议**: 寻找一个更可靠的关联方法。可以考虑对 `Telepad.OnAcceptDelivery` 进行 `Prefix` 和 `Postfix` 组合补丁。在 `Prefix` 中记录下 `ITelepadDeliverable` 的信息，在 `Postfix` 中（此时复制人已生成），通过比较 `MinionStartingStats` 的属性来精确匹配新生成的复制人。

- **问题描述**: 复制人压力崩溃类型被硬编码。
- **风险分析**: `DuplicantStressBreakPatch` 在广播压力崩溃事件时，将崩溃类型硬编码为 `"BingeEat"`。这无法准确反映游戏中实际发生的多种压力反应（如破坏、哭泣、呕吐等），向 API 客户端提供了错误的信息。
- **相关代码**:
  ```csharp
  // MOd/Patches/DuplicantLifecyclePatches.cs
  public static class DuplicantStressBreakPatch
  {
      public static void Postfix(StressMonitor.Instance __instance, ref bool __result)
      {
          // ...
          var payload = new
          {
              // ...
              breakType = "BingeEat" // This is an assumption
          };
          _eventBroadcaster?.BroadcastEvent("Alert.Duplicant.StressBreak", payload);
      }
  }
  ```
- **修改建议**: 需要通过 `__instance.GetCurrentReactable()` 来获取当前的压力反应（`Reactable`），并从中提取真实的崩溃类型 ID 或名称，而不是使用硬编码的字符串。

- **问题描述**: API 执行器中查找复制人的方式不够健壮。
- **风险分析**: `PersonnelExecutor` 主要通过 `minionIdentity.name` 或 `minionIdentity.GetProperName()` 来查找复制人。当玩家为复制人重命名后，依赖名字的查找方式会变得不可靠。最稳妥的标识符是 `KPrefabID.InstanceID`，它在游戏存档中是唯一的。当前实现虽然也检查 `name`（内部 ID），但将两者混用增加了不确定性。
- **相关代码**:
  ```csharp
  // MOd/API/Executors/PersonnelExecutor.cs
  var minionIdentity = Components.LiveMinionIdentities.Items.FirstOrDefault(m => m.name == duplicantId || m.GetProperName() == duplicantId);
  ```
- **修改建议**: API 应强制使用 `KPrefabID.InstanceID` 作为操作复制人的唯一标识符。修改 `PersonnelExecutor` 中的查找逻辑，使其仅通过 `InstanceID` 进行匹配。同时，API 文档和事件广播的 `duplicantId` 字段也应明确为 `InstanceID`。

- **问题描述**: 拆除建筑时，无法获取回收的资源列表。
- **风险分析**: `BuildingDeconstructedPatch` 在建筑被拆除后触发，此时获取被拆除建筑的构成材料变得困难。代码注释也承认了这一点，导致发送给 API 的事件信息不完整。
- **相关代码**:
  ```csharp
  // MOd/Patches/BuildingStatusPatches.cs
  public static class BuildingDeconstructedPatch
  {
      public static void Postfix(Deconstructable __instance, WorkerBase worker)
      {
          // ...
          // Note: A more robust solution might need to patch another method
          // to get materials before deconstruction. For now, we send an empty object.
          var salvaged = new Dictionary<string, float>();
          // ...
      }
  }
  ```
- **修改建议**: 使用 `Prefix` 补丁。在 `Deconstructable.OnCompleteWork` 执行 **之前**，通过 `__instance.gameObject.GetComponent<PrimaryElement>().MassPerUnit` 和其他相关组件，记录下建筑包含的元素和质量。将这些信息存储在一个临时变量中（例如 `static` 字典），然后在 `Postfix` 补丁中读取这些信息并发送。
