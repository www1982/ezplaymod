using EZPlay.GameState;

namespace EZPlay.Core.Interfaces
{
    public interface IGameStateManager
    {
        ColonyState LastKnownState { get; }
        void Tick();
        void UpdateState();
    }
}
