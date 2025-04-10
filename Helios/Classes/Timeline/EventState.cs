using System.Reflection;
using Helios.Interfaces;

namespace Helios.Utilities.Timeline
{
    public class EventState
    {
        public string validFrom { get; set; }
        public List<IEvent> activeEvents { get; set; }
        public EventInfo state { get; set; }
    }
}
