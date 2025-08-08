using System.Collections.Generic;

namespace EZPlay.API.Queries
{
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
}