using System.Collections.Generic;
using EZPlay.GameState;

namespace EZPlay.API.Queries
{
    /// <summary>
    /// 负责处理对游戏世界网格的查询。
    /// </summary>
    public static class GridQueryExecutor
    {
        public static Dictionary<int, CellInfo> GetCellsInfo(List<int> cells)
        {
            var result = new Dictionary<int, CellInfo>();
            foreach (var cell in cells)
            {
                if (!Grid.IsValidCell(cell)) continue;

                var element = Grid.Element[cell];
                var info = new CellInfo
                {
                    Cell = cell,
                    ElementId = element.id.ToString(),
                    ElementState = element.state.ToString(),
                    Mass = Grid.Mass[cell],
                    Temperature = Grid.Temperature[cell],
                    DiseaseName = "None", // Default value
                    DiseaseCount = Grid.DiseaseCount[cell]
                };

                byte diseaseIdx = Grid.DiseaseIdx[cell];
                if (diseaseIdx != 255 && diseaseIdx < Db.Get().Diseases.Count)
                {
                    info.DiseaseName = Db.Get().Diseases[diseaseIdx].Id;
                }

                // 遍历所有可能的图层，查找格子上的对象
                for (int i = 0; i < (int)ObjectLayer.NumLayers; i++)
                {
                    var go = Grid.Objects[cell, i];
                    if (go != null)
                    {
                        info.GameObjects.Add(go.GetProperName());
                    }
                }

                result[cell] = info;
            }
            return result;
        }
    }
}