using System;
using System.Collections.Generic;
using System.Reflection;
using EZPlay.Core;
using EZPlay.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EZPlay.API.Executors
{
    /// <summary>
    /// 反射执行器，负责解析API请求，并通过反射动态执行操作。
    /// </summary>
    public static class ReflectionExecutor
    {
        // 查找对象 (已更新为世界感知)
        public static List<string> FindObjects(JObject query)
        {
            var componentName = query["componentName"]?.Value<string>();
            if (string.IsNullOrEmpty(componentName))
                throw new ArgumentException("'componentName' is required for find_objects.");

            if (query["worldId"] == null)
                throw new ArgumentException("'worldId' is required for find_objects.");
            var worldId = query["worldId"].Value<int>();

            var foundObjects = new List<string>();

            // 修正：使用正确的组件集合名称，并为没有直接集合的组件提供备用查找方案
            switch (componentName)
            {
                case "Prioritizable":
                    foreach (var item in Components.Prioritizables.GetWorldItems(worldId))
                        foundObjects.Add(GameObjectManager.CacheObject(item.gameObject));
                    break;
                case "BuildingComplete":
                    foreach (var item in Components.BuildingCompletes.GetWorldItems(worldId))
                        foundObjects.Add(GameObjectManager.CacheObject(item.gameObject));
                    break;

                // 对于没有专用集合的白名单组件，使用全局查找然后按世界ID过滤
                case "Storage":
                    foreach (var item in UnityEngine.Object.FindObjectsOfType<Storage>())
                        if (item.GetMyWorldId() == worldId)
                            foundObjects.Add(GameObjectManager.CacheObject(item.gameObject));
                    break;
                case "PrimaryElement":
                    foreach (var item in UnityEngine.Object.FindObjectsOfType<PrimaryElement>())
                        if (item.GetMyWorldId() == worldId)
                            foundObjects.Add(GameObjectManager.CacheObject(item.gameObject));
                    break;
                case "TreeFilterable":
                    foreach (var item in UnityEngine.Object.FindObjectsOfType<TreeFilterable>())
                        if (item.GetMyWorldId() == worldId)
                            foundObjects.Add(GameObjectManager.CacheObject(item.gameObject));
                    break;

                default:
                    // 对于不在白名单中的组件，不执行任何操作以确保安全
                    break;
            }

            return foundObjects;
        }

        // 获取属性
        public static object GetProperty(string objectId, string componentName, string propertyName)
        {
            var go = GameObjectManager.GetObject(objectId);
            if (go == null) throw new Exception($"Object with ID '{objectId}' not found.");

            if (!SecurityWhitelist.AllowedComponents.Contains(componentName) || !SecurityWhitelist.AllowedProperties.Contains($"{componentName}.{propertyName}"))
                throw new UnauthorizedAccessException($"Access to property '{componentName}.{propertyName}' is denied.");

            var component = go.GetComponent(componentName);
            if (component == null) throw new Exception($"Component '{componentName}' not found.");

            var propInfo = component.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (propInfo == null) throw new Exception($"Property '{propertyName}' not found.");

            return propInfo.GetValue(component, null);
        }

        // 调用方法
        public static object CallMethod(string objectId, string componentName, string methodName, JArray args)
        {
            var go = GameObjectManager.GetObject(objectId);
            if (go == null) throw new Exception($"Object with ID '{objectId}' not found.");

            if (!SecurityWhitelist.AllowedComponents.Contains(componentName) || !SecurityWhitelist.AllowedMethods.Contains($"{componentName}.{methodName}"))
                throw new UnauthorizedAccessException($"Access to method '{componentName}.{methodName}' is denied.");

            var component = go.GetComponent(componentName);
            if (component == null) throw new Exception($"Component '{componentName}' not found.");

            // 简化的方法查找，未处理重载
            var methodInfo = component.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (methodInfo == null) throw new Exception($"Method '{methodName}' not found.");

            var methodParams = methodInfo.GetParameters();
            if (args.Count != methodParams.Length) throw new Exception("Incorrect number of parameters.");

            object[] convertedArgs = new object[args.Count];
            for (int i = 0; i < args.Count; i++)
            {
                convertedArgs[i] = ConvertJTokenToType(args[i], methodParams[i].ParameterType);
            }

            return methodInfo.Invoke(component, convertedArgs);
        }

        // 获取对象所有白名单内的详细信息
        public static Dictionary<string, Dictionary<string, object>> GetObjectDetails(string objectId)
        {
            var go = GameObjectManager.GetObject(objectId);
            if (go == null) throw new Exception($"Object with ID '{objectId}' not found.");

            var result = new Dictionary<string, Dictionary<string, object>>();

            foreach (var componentName in SecurityWhitelist.AllowedComponents)
            {
                var component = go.GetComponent(componentName);
                if (component == null) continue;

                var componentProperties = new Dictionary<string, object>();
                var properties = component.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var propInfo in properties)
                {
                    string fullPropName = $"{componentName}.{propInfo.Name}";
                    if (SecurityWhitelist.AllowedProperties.Contains(fullPropName))
                    {
                        try
                        {
                            componentProperties[propInfo.Name] = propInfo.GetValue(component, null);
                        }
                        catch { } // Ignore properties that might fail to get
                    }
                }

                if (componentProperties.Count > 0)
                {
                    result[componentName] = componentProperties;
                }
            }

            return result;
        }

        // 简化的类型转换器
        private static object ConvertJTokenToType(JToken token, Type targetType)
        {
            if (targetType == typeof(PrioritySetting))
            {
                var priorityClass = (PriorityScreen.PriorityClass)Enum.Parse(typeof(PriorityScreen.PriorityClass), token["priority_class"].Value<string>());
                var priorityValue = token["priority_value"].Value<int>();
                return new PrioritySetting(priorityClass, priorityValue);
            }
            if (targetType == typeof(Tag))
            {
                return new Tag(token.Value<string>());
            }
            return token.ToObject(targetType);
        }
    }
}