using System.Collections.Generic;

namespace EZPlay.Core
{
    /// <summary>
    /// 安全白名单，定义了AI可以通过反射API访问的组件和方法。
    /// 这是保障游戏稳定的最重要防线。
    /// </summary>
    public static class SecurityWhitelist
    {
        public static readonly HashSet<string> AllowedComponents = new HashSet<string>
        {
            "Storage", "PrimaryElement", "Prioritizable", "BuildingComplete", "TreeFilterable"
        };

        public static readonly HashSet<string> AllowedMethods = new HashSet<string>
        {
            // Prioritizable
            "Prioritizable.SetMasterPriority",
            // Storage
            "Storage.SetOnlyFetchMarkedItems",
            "Storage.allowItemRemoval",
            // TreeFilterable
            "TreeFilterable.AddTagToFilter",
            "TreeFilterable.RemoveTagFromFilter",
            "TreeFilterable.UpdateFilters"
        };

        public static readonly HashSet<string> AllowedProperties = new HashSet<string>
        {
            // PrimaryElement
            "PrimaryElement.Temperature",
            "PrimaryElement.Mass",
            "PrimaryElement.ElementID",
            // Storage
            "Storage.capacity",
            "Storage.MassStored",
            // Prioritizable
            "Prioritizable.masterPriority"
        };
    }
}