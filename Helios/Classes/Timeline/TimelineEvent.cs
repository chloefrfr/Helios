using Helios.Interfaces;

namespace Helios.Utilities.Timeline;

public class TimelineEvent : IEvent
{
    public string EventType { get; set; }
    public DateTime ActiveUntil { get; set; }
    public string ActiveSince { get; set; }
}