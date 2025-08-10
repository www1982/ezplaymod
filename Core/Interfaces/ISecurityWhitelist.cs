namespace EZPlay.Core.Interfaces
{
    public interface ISecurityWhitelist
    {
        bool IsAllowed(string typeName, string memberName);
    }
}