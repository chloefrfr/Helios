namespace Helios.Interfaces;

public interface IEvent
{
    string EventType { get; }
    DateTime ActiveUntil { get; }
    string ActiveSince { get; }
}