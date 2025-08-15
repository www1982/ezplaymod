using System;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace EZPlay.API.Queries
{
    public static class FindObjectsQueryExecutor
    {
        private class FindRequest
        {
            public string ComponentName { get; set; }
        }

        public static object Execute(int worldId, string jsonPayload)
        {
            var request = JsonConvert.DeserializeObject<FindRequest>(jsonPayload);
            if (request == null || string.IsNullOrEmpty(request.ComponentName))
            {
                throw new ArgumentException("Invalid payload for find_objects query.");
            }

            var componentType = Type.GetType($"UnityEngine.{request.ComponentName}, UnityEngine");
            if (componentType == null)
            {
                // Search in Assembly-CSharp as well
                componentType = Type.GetType($"{request.ComponentName}, Assembly-CSharp");
            }

            if (componentType == null)
            {
                throw new ArgumentException($"Component type '{request.ComponentName}' not found.");
            }

            var objects = Resources.FindObjectsOfTypeAll(componentType)
                .OfType<Component>()
                .Where(c => c.gameObject.GetMyWorldId() == worldId)
                .Select(c => new
                {
                    GameObjectId = c.gameObject.GetInstanceID(),
                    GameObjectName = c.gameObject.name,
                    Position = c.transform.position
                })
                .ToList();

            return objects;
        }
    }
}