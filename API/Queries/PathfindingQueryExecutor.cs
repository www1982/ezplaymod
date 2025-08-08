using System;
using System.Collections.Generic;
using EZPlay.Utils;

namespace EZPlay.API.Queries
{
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
}