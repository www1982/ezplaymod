using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// =========================================================================
// VII. 蓝图系统 - 数据模型
// =========================================================================

/// <summary>
/// 蓝图的核心容器。
/// </summary>
public class Blueprint
{
    public string Name { get; set; }
    public string Author { get; set; }
    public Vector2I Dimensions { get; set; }
    public List<BlueprintItem> Buildings { get; set; } = new List<BlueprintItem>();
    public List<BlueprintItem> Tiles { get; set; } = new List<BlueprintItem>();
    public List<BlueprintItem> Others { get; set; } = new List<BlueprintItem>();
}

/// <summary>
/// 定义蓝图中每一个独立的对象。
/// </summary>
public class BlueprintItem
{
    public string PrefabID { get; set; }
    public Vector2I Offset { get; set; }
    public SimHashes Element { get; set; }
    public Orientation Orientation { get; set; }
    public Dictionary<string, JToken> Settings { get; set; } = new Dictionary<string, JToken>();
}
