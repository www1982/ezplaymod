# 缺氧 (Oxygen Not Included) 外部 API 模组

本项目是一个为游戏《缺氧》 (Oxygen Not Included) 设计的复杂模组 (Mod)，其核心目标是提供一个强大而灵活的外部应用程序接口 (API)。通过此 API，第三方工具、脚本或应用程序可以实时监控和控制游戏内部的状态，从而实现自动化、数据分析或与其他系统的集成。

## 主要功能模块

该模组由多个协同工作的模块组成，每个模块负责一部分特定功能：

- ### `Core` - 核心模块

  负责整个 Mod 的基础架构，包括初始化、日志记录以及核心服务的管理。它是所有其他模块运行的基础。

- ### `Patches` - 补丁模块

  此模块通过对游戏原始代码进行“修补” (Patching)，在不直接修改游戏文件的前提下，注入自定义逻辑。这使得 Mod 能够监听和捕获游戏内的各种关键事件，例如：

  - 复制人任务分配与完成
  - 科技研究进度
  - 游戏内警报触发
  - 其他重要的状态变更

- ### `API` - 应用程序接口模块

  这是与外部工具交互的核心。它建立一个服务器来接收外部指令，并将其转化为游戏内的具体操作。该模块主要包含：

  - **`EventSocketServer`**: 一个用于实时事件推送的 WebSocket 服务器，可将游戏内发生的事件（如警报、任务完成等）实时广播给连接的客户端。
  - **`Executors`**: 负责处理“写操作”的执行器。它们接收指令并执行具体游戏操作，如建造建筑、拆除设施、管理人员等。
  - **`Queries`**: 负责处理“读操作”的查询器。它们用于响应外部请求，查询游戏当前状态的各种信息。

- ### `Blueprints` - 蓝图模块

  专注于管理和操作游戏中的建筑蓝图。它允许外部工具扫描现有的建筑布局、保存为蓝图，并根据蓝图进行自动化建造。

- ### `Logistics` - 物流模块

  此模块专门用于管理游戏内的物流系统，包括监控物资运输状态、调整运输优先级以及优化资源分配。

- ### `GameState` - 游戏状态模块
  维护一个从 Mod 视角观察到的游戏当前状态的快照。这个快照为 API 的查询功能 (`Queries`) 提供了快速、可靠的数据支持，避免了频繁直接查询游戏引擎带来的性能开销。

## 架构设计

为了提高代码的可维护性、可测试性和灵活性，本项目采用**依赖注入 (Dependency Injection, DI)** 和**面向接口编程**的设计原则。

- **接口定义 (`Core/Interfaces`)**: 核心服务（如日志记录、事件广播）的行为被定义为接口（例如 `ILogger`, `IEventBroadcaster`）。这使得任何模块都可以依赖于这些抽象的“契约”，而不是具体的实现类。

- **服务容器 (`Core/ServiceContainer`)**: 一个简单的静态服务容器负责在 Mod 启动时注册所有核心服务的实例。

- **依赖解析**: 在代码的任何地方，当需要使用一个核心服务时，都通过向 `ServiceContainer` 请求相应的接口来获取服务实例。这种方式取代了硬编码的静态类引用，从而实现了模块间的松耦合。

这种架构带来了以下优势：

- **高可测试性**: 在单元测试中，可以轻松地用“模拟 (Mock)”实现来替换真实的服务，从而实现对业务逻辑的隔离测试。
- **清晰的依赖关系**: 模块所需的服务在其代码中被明确请求，使得代码结构更清晰。
- **灵活的实现替换**: 未来可以方便地替换或装饰任何核心服务的具体实现，而无需修改依赖它的代码。

## AI 事件感知系统

本 Mod 实现了一个全面的事件系统，能够感知游戏中的各种关键变化，并将它们作为结构化的 JSON 数据通过 WebSocket 实时广播。这为外部 AI 或自动化工具提供了决策所需的核心数据。

### I. 小人生命周期与状态事件 (Duplicant Lifecycle & Status)

- **`Lifecycle.Duplicant.Printed`**: 当一个新的复制人被打印（选择）时触发。
- **`Lifecycle.Duplicant.Death`**: 当一个复制人死亡时触发。
- **`Lifecycle.Duplicant.GainedSkill`**: 当一个复制人掌握一项新技能时触发。
- **`Alert.Duplicant.StressBreak`**: 当一个复制人压力过大并崩溃时触发。
- **`Alert.Dulicant.DiseaseGained`**: 当一个复制人感染疾病时触发。

### II. 建筑与设施状态事件 (Building & Facility Status)

- **`Alert.Building.Broken`**: 当一个建筑损坏时触发。
- **`Alert.Building.Overheated`**: 当一个建筑过热时触发。
- **`Milestone.Building.Deconstructed`**: 当一个建筑被完全拆除时触发。
- **`StateChange.Storage.ContentChanged`**: 当一个存储容器的内容发生变化时触发。

### III. 资源与环境突变事件 (Resource & Environment)

- **`StateChange.Geyser.EruptionStateChanged`**: 当一个间歇泉开始或停止喷发时触发。
- **`Milestone.World.NewElementDiscovered`**: 当一种新的元素被发现时触发。
- **`Alert.World.MeteorShower`**: 当一场流星雨开始时触发。

### IV. 里程碑与全局状态事件 (Milestone & Global State)

- **`Milestone.PrintingPod.NewPrintablesAvailable`**: 当打印舱提供新的可打印选项时触发。
- **`Milestone.Artifact.Analyzed`**: 当一个神器被成功分析时触发。
- **`StateChange.Schedule.Changed`**: 当一个日程表被修改时触发。
