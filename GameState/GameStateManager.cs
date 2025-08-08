using Klei.AI;

namespace EZPlay.GameState
{
    public static class GameStateManager
    {
        public static ColonyState LastKnownState { get; private set; }
        public static void UpdateState()
        {
            if (SaveLoader.Instance == null || !SaveLoader.Instance.loadedFromSave || ClusterManager.Instance == null) return;

            var colonyState = new ColonyState
            {
                Cycle = GameClock.Instance.GetCycle(),
                TimeInCycle = GameClock.Instance.GetTimeInCycles()
            };

            // --- 全局研究进度 ---
            if (Research.Instance != null && Db.Get().Techs != null)
            {
                var activeResearch = Research.Instance.GetActiveResearch();
                foreach (var tech in Db.Get().Techs.resources)
                {
                    var techInstance = Research.Instance.GetTechInstance(tech.Id);
                    string status = "Locked";
                    if (techInstance != null && techInstance.IsComplete()) status = "Completed";
                    else if (tech.ArePrerequisitesComplete()) status = "Available";

                    colonyState.ResearchState.Add(new TechData
                    {
                        Id = tech.Id,
                        Name = tech.Name,
                        Status = status,
                        ProgressPercent = (activeResearch != null && activeResearch.tech == tech) ? activeResearch.GetTotalPercentageComplete() : 0f
                    });
                }
            }

            // --- 全局最新一份每日报告 ---
            var reportManager = ReportManager.Instance;
            if (reportManager != null && reportManager.reports.Count > 0)
            {
                var latestReport = reportManager.reports[reportManager.reports.Count - 1];
                colonyState.LatestDailyReport = new DailyReportData
                {
                    CycleNumber = latestReport.day,
                    OxygenChange = latestReport.GetEntry(ReportManager.ReportType.OxygenCreated).Net,
                    CalorieChange = latestReport.GetEntry(ReportManager.ReportType.CaloriesCreated).Net,
                    PowerChange = latestReport.GetEntry(ReportManager.ReportType.EnergyCreated).Net
                };
            }

            // --- 按星球分类收集数据 ---
            foreach (var world in ClusterManager.Instance.WorldContainers)
            {
                if (!world.IsDiscovered) continue;

                var worldState = new WorldState
                {
                    WorldId = world.id,
                    WorldName = world.GetProperName()
                };

                var inventory = world.worldInventory;

                // --- 资源 ---
                foreach (Element element in ElementLoader.elements)
                {
                    if (element != null && element.id != SimHashes.Vacuum)
                    {
                        float amount = inventory.GetAmountWithoutTag(element.tag);
                        if (amount > 0) worldState.Resources[element.name] = amount;
                    }
                }

                // --- 复制人 ---
                foreach (var minion in Components.MinionIdentities.GetWorldItems(world.id))
                {
                    if (minion == null) continue;
                    var dupeState = new DuplicantState { Name = minion.name.Replace("(Clone)", "") };
                    dupeState.Stress = minion.GetAmounts().Get(Db.Get().Amounts.Stress)?.value ?? 0;
                    dupeState.Health = minion.GetComponent<Health>()?.hitPoints ?? 0;
                    worldState.Duplicants.Add(dupeState);
                }

                // --- 殖民地概要 (按星球) ---
                worldState.ColonySummary = new ColonySummaryData
                {
                    DuplicantCount = Components.MinionIdentities.GetWorldItems(world.id).Count,
                    Calories = inventory.GetAmountWithoutTag(GameTags.Edible)
                };
                foreach (Brain brain in Components.Brains.GetWorldItems(world.id))
                {
                    if (brain != null && brain.gameObject != null && !brain.gameObject.HasTag(GameTags.BaseMinion))
                    {
                        string name = brain.gameObject.GetProperName();
                        if (worldState.ColonySummary.CritterCount.ContainsKey(name)) worldState.ColonySummary.CritterCount[name]++;
                        else worldState.ColonySummary.CritterCount[name] = 1;
                    }
                }

                colonyState.Worlds[world.id] = worldState;
            }

            LastKnownState = colonyState;
        }
    }
}