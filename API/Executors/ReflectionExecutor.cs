using System;
using System.Linq;
using System.Reflection;
using EZPlay.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace EZPlay.API.Executors
{
    public static class ReflectionExecutor
    {
        private class ReflectionRequest
        {
            public int GameObjectId { get; set; }
            public string ComponentName { get; set; }
            public string MemberName { get; set; }
            public object[] Parameters { get; set; }
            public bool IsProperty { get; set; }
        }

        public static object Execute(string jsonPayload)
        {
            var request = JsonConvert.DeserializeObject<ReflectionRequest>(jsonPayload);
            if (request == null)
            {
                throw new ArgumentException("Invalid payload for reflection.");
            }

            if (!SecurityWhitelist.AllowedComponents.Contains(request.ComponentName))
                throw new UnauthorizedAccessException($"Access to component '{request.ComponentName}' is not allowed.");

            var go = GetGameObjectById(request.GameObjectId);
            if (go == null)
                throw new ArgumentException($"GameObject with ID '{request.GameObjectId}' not found.");

            var component = go.GetComponent(request.ComponentName);
            if (component == null)
                throw new ArgumentException($"Component '{request.ComponentName}' not found on GameObject.");

            if (request.IsProperty)
            {
                if (!SecurityWhitelist.AllowedProperties.Contains($"{request.ComponentName}.{request.MemberName}"))
                    throw new UnauthorizedAccessException($"Access to property '{request.MemberName}' is not allowed.");

                var prop = component.GetType().GetProperty(request.MemberName, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null)
                    throw new MissingMemberException(request.ComponentName, request.MemberName);

                return prop.GetValue(component, null);
            }
            else
            {
                if (!SecurityWhitelist.AllowedMethods.Contains($"{request.ComponentName}.{request.MemberName}"))
                    throw new UnauthorizedAccessException($"Access to method '{request.MemberName}' is not allowed.");

                var method = component.GetType().GetMethod(request.MemberName, BindingFlags.Public | BindingFlags.Instance);
                if (method == null)
                    throw new MissingMemberException(request.ComponentName, request.MemberName);

                return method.Invoke(component, request.Parameters);
            }
        }

        private static GameObject GetGameObjectById(int id)
        {
            // Correct way to find all GameObjects, as GameObject is not a Component.
            return Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(go => go.GetInstanceID() == id);
        }
    }
}