using System;
using Newtonsoft.Json;

namespace EZPlay.API.Queries
{
    public static class PathfindingQueryExecutor
    {
        private class PathfindingRequest
        {
            public int StartX { get; set; }
            public int StartY { get; set; }
            public int EndX { get; set; }
            public int EndY { get; set; }
        }

        public static object Execute(int worldId, string jsonPayload)
        {
            var request = JsonConvert.DeserializeObject<PathfindingRequest>(jsonPayload);
            if (request == null)
            {
                throw new ArgumentException("Invalid payload for pathfinding query.");
            }

            var originalWorldId = ClusterManager.Instance.activeWorldId;
            try
            {
                ClusterManager.Instance.SetActiveWorld(worldId);
                var startCell = Grid.PosToCell(new UnityEngine.Vector3(request.StartX, request.StartY, 0));
                var endCell = Grid.PosToCell(new UnityEngine.Vector3(request.EndX, request.EndY, 0));

                // Simplified check: Are the cells in the same room?
                // This is a rough approximation of pathfinding but avoids complex API usage.
                var roomProber = Game.Instance.roomProber;
                var startCavity = roomProber.GetCavityForCell(startCell);
                var endCavity = roomProber.GetCavityForCell(endCell);
                bool pathExists = (startCavity != null && startCavity == endCavity);

                return new
                {
                    PathExists = pathExists,
                    Cost = -1 // Cost is not calculated in this simplified version
                };
            }
            finally
            {
                ClusterManager.Instance.SetActiveWorld(originalWorldId);
            }
        }
    }
}