using System.Collections.Generic;

namespace EZPlay.GameState
{
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
    /// 标准化的事件“信封”，用于通过WebSocket进行广播。
    /// </summary>
    public class GameEvent
    {
        /// <summary>
        /// 事件的唯一标识符, e.g., "Milestone.ResearchComplete"
        /// </summary>
        public string EventType { get; set; }

        /// <summary>
        /// 事件发生时的游戏周期
        /// </summary>
        public int Cycle { get; set; }

        /// <summary>
        /// 事件相关的具体数据
        /// </summary>
        public object Payload { get; set; }
    }
}