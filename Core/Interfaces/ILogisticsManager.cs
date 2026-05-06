using EZPlay.Logistics;

namespace EZPlay.Core.Interfaces
{
    public interface ILogisticsManager
    {
        void RegisterPolicy(LogisticsPolicy policy);
        void UnregisterPolicy(string policyId);
        void Tick(float deltaTime);
    }
}
