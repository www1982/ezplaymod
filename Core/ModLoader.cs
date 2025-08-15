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
            base.OnLoad(harmony);

            // 1. Instantiate and register Logger
            var logger = new Logger("ModLoader");
            Logger.CurrentLogLevel = LogLevel.DEBUG;
            ServiceContainer.Register<EZPlay.Core.Interfaces.ILogger>(logger);

            logger.Info("ModLoader loaded.");

            // Load config
            string configPath = "Mods/EZPlay/config.json";
            int apiPort = 8080;
            int eventPort = 8081;
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);
                apiPort = config.GetValueOrDefault("api_port", 8080);
                eventPort = config.GetValueOrDefault("event_port", 8081);
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

            MainThreadDispatcher.OnUpdate += () => LogisticsManager.Tick(Time.deltaTime);
        }
    }
}