using HarmonyLib;
using EZPlay.Utils;

namespace EZPlay.Patches
{
    // 在加载新游戏时，清空对象缓存，避免ID冲突和内存泄漏
    [HarmonyPatch(typeof(Game), "Load")]
    public class GameLoadPatch
    {
        public static void Postfix() => GameObjectManager.Clear();
    }
}