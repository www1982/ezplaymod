using HarmonyLib;
using EZPlay.API;
using EZPlay.Core.Interfaces;
using EZPlay.Logistics;
using EZPlay.Utils;
using KMod;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace EZPlay.Core
{
    public class ModLoader : UserMod2
    {
        public const string ApiVersion = "1.1.0";

        public override void OnLoad(Harmony harmony)
        {
            // Do NOT call base.OnLoad(harmony) because it uses the unsafe harmony.PatchAll()
            // which crashes the entire mod if a single patch fails due to game updates.
            
            // 1. Instantiate and register Logger
            var logger = new Logger("ModLoader");
            Logger.CurrentLogLevel = LogLevel.DEBUG;
            ServiceContainer.Register<EZPlay.Core.Interfaces.ILogger>(logger);

            logger.Info("ModLoader loading... Starting safe Harmony patching.");

            // 1.5 Safe Patching
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            foreach (var type in assembly.GetTypes())
            {
                var harmonyMethods = type.GetCustomAttributes(typeof(HarmonyPatch), false);
                if (harmonyMethods != null && harmonyMethods.Length > 0)
                {
                    try
                    {
                        var processor = harmony.CreateClassProcessor(type);
                        processor.Patch();
                    }
                    catch (System.Exception ex)
                    {
                        logger.Warning($"[SafePatching] Failed to apply patch for {type.Name}. It will be disabled. Reason: {ex.Message}");
                    }
                }
            }
            logger.Info("Safe patching complete.");

            // Load config
            string configPath = "Mods/EZPlay/config.json";
            int apiPort = 8080;
            int eventPort = 8081;
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
                if (config.TryGetValue("api_port", out int aPort)) apiPort = aPort;
                if (config.TryGetValue("event_port", out int ePort)) eventPort = ePort;
            }

            // 2. Instantiate and register SecurityWhitelist
            var whitelist = new SecurityWhitelist(logger, "Mods/EZPlay/whitelist.json");
            ServiceContainer.Register<ISecurityWhitelist>(whitelist);

            // 3. Start the API server
            ApiServer.Start(apiPort);

            // 4. Instantiate and register EventSocketServer
            var eventServer = new EventSocketServer($"ws://0.0.0.0:{eventPort}");
            ServiceContainer.Register<IEventBroadcaster>(eventServer);
            eventServer.Start();

            // 5. Instantiate and register GameStateManager
            var gameStateManager = new EZPlay.GameState.GameStateManager();
            ServiceContainer.Register<IGameStateManager>(gameStateManager);

            // 6. Instantiate and register LogisticsManager
            var logisticsManager = new LogisticsManager();
            ServiceContainer.Register<ILogisticsManager>(logisticsManager);

            MainThreadDispatcher.OnUpdate += () => logisticsManager.Tick(Time.deltaTime);
        }
    }
}