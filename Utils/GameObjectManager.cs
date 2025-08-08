using System;
using System.Collections.Generic;
using UnityEngine;

namespace EZPlay.Utils
{
    /// <summary>
    /// 管理游戏对象的缓存，将 GameObject 实例映射到安全的字符串ID。
    /// </summary>
    public static class GameObjectManager
    {
        // 使用 WeakReference 防止我们的缓存阻止游戏对象被垃圾回收
        private static readonly Dictionary<string, WeakReference<GameObject>> CachedObjects = new Dictionary<string, WeakReference<GameObject>>();

        public static string CacheObject(GameObject go)
        {
            if (go == null) return null;
            string id = Guid.NewGuid().ToString();
            CachedObjects[id] = new WeakReference<GameObject>(go);
            return id;
        }

        public static GameObject GetObject(string id)
        {
            if (CachedObjects.TryGetValue(id, out var weakRef))
            {
                if (weakRef.TryGetTarget(out var go)) return go;
                CachedObjects.Remove(id); // 对象已被销毁，从缓存移除
            }
            return null;
        }

        public static void Clear() => CachedObjects.Clear();
    }
}