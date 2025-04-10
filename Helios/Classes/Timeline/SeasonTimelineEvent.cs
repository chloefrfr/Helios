using Helios.Interfaces;

namespace Helios.Utilities.Timeline;

public class SeasonTimelineEvent
{
    public int SeasonNumber { get; set; }
    public List<IEvent> Events { get; set; } = new();
}