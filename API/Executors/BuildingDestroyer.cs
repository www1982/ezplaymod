using System;
using Newtonsoft.Json;
using UnityEngine;

namespace EZPlay.API.Executors
{
    public static class BuildingDestroyer
    {
        private class DestroyRequest
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        public static object Execute(string jsonPayload)
        {
            var request = JsonConvert.DeserializeObject<DestroyRequest>(jsonPayload);
            if (request == null)
            {
                throw new ArgumentException("Invalid payload for destroying building.");
            }

            var cell = Grid.PosToCell(new Vector3(request.X, request.Y, 0));
            var building = Grid.Objects[cell, (int)ObjectLayer.Building];

            if (building != null)
            {
                // Use KDestroyGameObject for immediate and safe removal.
                Util.KDestroyGameObject(building);
                return new { success = true, message = $"Building at ({request.X}, {request.Y}) destroyed." };
            }

            return new { success = false, message = $"No building found at ({request.X}, {request.Y})." };
        }
    }
}