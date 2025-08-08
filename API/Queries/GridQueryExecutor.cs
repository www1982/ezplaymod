using System;
using Newtonsoft.Json;
using UnityEngine;

namespace EZPlay.API.Queries
{
    public static class GridQueryExecutor
    {
        private class GridQueryRequest
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        public static object Execute(string jsonPayload)
        {
            var request = JsonConvert.DeserializeObject<GridQueryRequest>(jsonPayload);
            if (request == null)
            {
                throw new ArgumentException("Invalid payload for grid query.");
            }

            var cell = Grid.PosToCell(new Vector3(request.X, request.Y, 0));
            var element = Grid.Element[cell];
            var temperature = Grid.Temperature[cell];
            var mass = Grid.Mass[cell];
            var building = Grid.Objects[cell, (int)ObjectLayer.Building];

            return new
            {
                Element = element.id.ToString(),
                Temperature = temperature,
                Mass = mass,
                BuildingName = building != null ? building.GetProperName() : "None"
            };
        }
    }
}