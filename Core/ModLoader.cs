using HarmonyLib;
using EZPlay.API;
using EZPlay.Core.Interfaces;
using EZPlay.Logistics;
using EZPlay.Utils;
using KMod;
using UnityEngine;

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

            // 2. Start the API server (if it's static and doesn't need registration)
            ApiServer.Start();

            // 3. Instantiate and register EventSocketServer
            var eventServer = new EventSocketServer("ws://0.0.0.0:8081");
            ServiceContainer.Register<IEventBroadcaster>(eventServer);
            eventServer.Start();

            MainThreadDispatcher.OnUpdate += () => LogisticsManager.Tick(Time.deltaTime);
        }
    }
}