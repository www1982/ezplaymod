using HarmonyLib;
using EZPlay.API;
using KMod;

namespace EZPlay.Core
{
    public class ModLoader : UserMod2
    {
        private static ApiServer _apiServer;

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);

            // API服务器现在监听 /api/ 根路径
            _apiServer = new ApiServer("http://localhost:8080/api/");
            _apiServer.Start();
        }
    }
}