using HarmonyLib;
using EZPlay.API;
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

            var logger = new EZPlay.Core.Logger("ModLoader");
            EZPlay.Core.Logger.CurrentLogLevel = LogLevel.DEBUG;
            logger.Info("ModLoader loaded.");

            ApiServer.Start();

            var eventServer = new EventSocketServer("ws://0.0.0.0:8081");
            ServiceLocator.Register(eventServer);
            eventServer.Start();

            MainThreadDispatcher.OnUpdate += () => LogisticsManager.Tick(Time.deltaTime);
        }
    }
}