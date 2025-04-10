﻿namespace Helios.Utilities.Timeline
{
    public class EventInfo
    {
        public List<string> activeStorefronts { get; set; }
        public Dictionary<string, object> eventNamedWeights { get; set; }
        public int seasonNumber { get; set; }
        public string seasonTemplateId { get; set; }
        public int matchXpBonusPoints { get; set; }
        public string seasonBegin { get; set; }
        public string seasonEnd { get; set; }
        public string seasonDisplayedEnd { get; set; }
        public string weeklyStoreEnd { get; set; }
        public string stwEventStoreEnd { get; set; }
        public string stwWeeklyStoreEnd { get; set; }
        public Dictionary<string, string> sectionStoreEnds { get; set; }
        public string dailyStoreEnd { get; set; }
    }
}
