# EZPlay MOD 框架

**EZPlay** 是一个为游戏《缺氧》(Oxygen Not Included) 设计的高度集成的 MOD 框架。其核心目标是通过提供一组强大的外部 API，允许第三方程序（如 AI、脚本、管理工具）与游戏世界进行实时、双向的交互。

该框架将复杂的《缺氧》MOD 开发流程抽象化，为外部 AI 或自动化脚本提供了一个前所未有的、事件驱动的神经接口，使其能够从“定期检查”的被动模式进化为“事件驱动”的主动模式，实现毫秒级的反应速度和更智能的决策。

## 核心特性

- **双通道通信架构**:
  - **指令通道 (WebSocket @ `ws://localhost:8080`)**: 用于外部客户端向游戏发送查询和执行指令。
  - **事件通道 (WebSocket @ `ws://localhost:8081`)**: 用于 MOD 主动、实时地向所有连接的客户端推送游戏内发生的关键事件。
- **事件驱动**: 通过 `Harmony` 补丁精确捕获游戏内数十种关键事件（警报、里程碑、状态变更），使外部 AI 能够对游戏世界的变化做出即时响应。
- **线程安全**: 内置强大的主线程调度器，确保所有来自外部的异步请求都能安全地在游戏主线程中执行，杜绝了多线程冲突的风险。
- **高度安全**: 采用严格的白名单机制，精确控制外部 API 可访问的游戏组件、方法和属性，在提供强大功能的同时，最大限度地保障了游戏的稳定性和安全性。
- **标准化数据模型**: 将复杂的游戏状态和事件信息封装成结构清晰、易于解析的 JSON 对象，大大降低了客户端的开发难度。

## 事件驱动系统详解

这是 EZPlay 框架的核心。外部 AI 不再需要频繁地轮询游戏状态，而是可以订阅一个持久的事件流。

### 事件格式

所有事件都遵循统一的 `GameEvent` "信封"格式：

```json
{
  "EventType": "事件类型 (string)",
  "Cycle": "发生周期 (int)",
  "Payload": {
    "事件具体数据": "..."
  }
}
```

- **EventType**: 一个唯一的、点分隔的事件标识符，便于客户端进行分类和过滤。
- **Cycle**: 事件发生时的游戏内周期，为每个事件自动打上时间戳。
- **Payload**: 一个灵活的数据载体，用于存放与该事件相关的具体信息。

### 已实现的事件列表

#### 1. 警报事件 (Alerts) - _需要立即响应_

- `Alert.DuplicantSuffocating`
- `Alert.DuplicantStarving`
- `Alert.BuildingOverheating`

#### 2. 里程碑事件 (Milestones) - _影响长期战略_

- `Milestone.ResearchComplete`
- `Milestone.NewDuplicantPrinted`

#### 3. 状态变更事件 (State Changes) - _提供环境信息_

- `StateChange.GeyserStateChanged`

## API 参考 (指令通道)

指令通道用于查询游戏状态或执行操作。所有请求都应发送到 `ws://localhost:8080/api`。

### 请求格式

```json
{
  "Action": "要执行的动作 (string)",
  "Payload": {
    "动作所需参数": "..."
  }
}
```

### 主要 Action

- **`state`**: 获取当前完整的游戏状态快照 (无需 Payload)。

#### 查询 (Queries)

- **`find_objects`**: 查找符合条件的游戏对象。
- **`grid`**: 查询指定坐标格子的详细信息。
- **`pathfinding`**: 查询两点之间的寻路信息。
- **`chore_status`**: 查询特定任务的状态。

#### 执行器 (Executors)

- **`execute_global_action`**: 执行一个全局动作 (例如暂停、加速)。
- **`execute_reflection`**: 在安全白名单的约束下，执行反射调用以修改游戏对象。
- **`place_blueprint`**: 根据蓝图数据在游戏中进行建造。
- **`destroy_building`**: 销毁指定的建筑。

## 开发与构建

### 环境要求

- .NET Framework 4.7.2
- 《缺氧》游戏本体

### 构建流程

1.  确保 `EZPlay.csproj` 文件中的游戏库引用路径正确。
2.  在 `MOd/` 目录下运行 `dotnet build`。
3.  成功后，将 `MOd/bin/Debug/net472/EZPlay.dll` 复制到游戏的 MOD 目录中。

---

_该 README 文档由 Roo 自动生成。_
