namespace EZPlay.Core.Interfaces
{
    public interface IEventBroadcaster
    {
        void BroadcastEvent(string eventType, object payload);
    }
}