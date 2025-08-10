using System;
using System.Linq;
using System.Reflection;
using EZPlay.API.Models;
using EZPlay.API.Exceptions;
using EZPlay.Core;
using EZPlay.Core.Interfaces;
using Newtonsoft.Json;
using UnityEngine;

namespace EZPlay.API.Executors
{
    public static class ReflectionExecutor
    {
        private static readonly EZPlay.Core.Logger logger = new EZPlay.Core.Logger("ReflectionExecutor");
        private static ISecurityWhitelist _whitelist;

        private class ReflectionRequest
        {
            public int GameObjectId { get; set; }
            public string ComponentName { get; set; }
            public string MemberName { get; set; }
            public object[] Parameters { get; set; }
            public bool IsProperty { get; set; }
        }

        public static ExecutionResult Execute(string jsonPayload)
        {
            _whitelist = ServiceContainer.Resolve<ISecurityWhitelist>();

            if (string.IsNullOrEmpty(jsonPayload))
            {
                throw new ApiException(400, "Payload cannot be null or empty.");
            }

            ReflectionRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<ReflectionRequest>(jsonPayload);
            }
            catch (JsonException ex)
            {
                throw new ApiException(400, $"Invalid JSON format: {ex.Message}");
            }

            if (request == null)
            {
                throw new ApiException(400, "Invalid payload structure for reflection.");
            }

            if (string.IsNullOrEmpty(request.ComponentName) || string.IsNullOrEmpty(request.MemberName))
            {
                throw new ApiException(400, "'ComponentName' and 'MemberName' are required.");
            }

            if (!_whitelist.IsAllowed(request.ComponentName, request.MemberName))
            {
                throw new ApiException(403, $"Access to member '{request.MemberName}' on component '{request.ComponentName}' is not allowed.");
            }

            var go = GetGameObjectById(request.GameObjectId);
            if (go == null)
            {
                throw new ApiException(404, $"GameObject with ID '{request.GameObjectId}' not found.");
            }

            var component = go.GetComponent(request.ComponentName);
            if (component == null)
            {
                throw new ApiException(404, $"Component '{request.ComponentName}' not found on GameObject with ID '{request.GameObjectId}'.");
            }

            try
            {
                if (request.IsProperty)
                {
                    var prop = component.GetType().GetProperty(request.MemberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (prop == null)
                    {
                        throw new ApiException(404, $"Property '{request.MemberName}' not found on component '{request.ComponentName}'.");
                    }

                    return new ExecutionResult { Success = true, Data = prop.GetValue(component, null) };
                }
                else
                {
                    var method = component.GetType().GetMethod(request.MemberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (method == null)
                    {
                        throw new ApiException(404, $"Method '{request.MemberName}' not found on component '{request.ComponentName}'.");
                    }

                    return new ExecutionResult { Success = true, Data = method.Invoke(component, request.Parameters) };
                }
            }
            catch (Exception ex)
            {
                throw new ApiException(500, $"An error occurred during reflection: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private static GameObject GetGameObjectById(int id)
        {
            // Correct way to find all GameObjects, as GameObject is not a Component.
            return Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(go => go.GetInstanceID() == id);
        }
    }
}