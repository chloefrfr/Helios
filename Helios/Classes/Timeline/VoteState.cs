﻿
namespace Helios.Utilities.Timeline
{
    public class VoteState
    {
        public string validFrom { get; set; }
        public List<string> activeEvents { get; set; }
        public ElectionState state { get; set; }
    }
}
