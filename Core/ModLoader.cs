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
        public static EventSocketServer EventServer { get; private set; }

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            ApiServer.Start();

            EventServer = new EventSocketServer("ws://0.0.0.0:8081");
            EventServer.Start();

            MainThreadDispatcher.OnUpdate += () => LogisticsManager.Tick(Time.deltaTime);
        }
    }
}