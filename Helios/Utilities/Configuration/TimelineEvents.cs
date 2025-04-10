using Helios.Interfaces;
using Helios.Utilities.Timeline;

namespace Helios.Configuration;

public class TimelineEvents
{
    public static readonly List<SeasonTimelineEvent> SeasonEvents = new()
    {
        new SeasonTimelineEvent
        {
            SeasonNumber = 3,
            Events = new List<IEvent>
            {
                new TimelineEvent
                {
                    EventType = "EventFlag.Spring2018Phase1",
                    ActiveUntil = Constants.ActiveUntil,
                    ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }
            }
        },
        new SeasonTimelineEvent
        {
            SeasonNumber = 4,
            Events = new List<IEvent>
            {
                new TimelineEvent
                {
                    EventType = "EventFlag.Blockbuster2018",
                    ActiveUntil = Constants.ActiveUntil,
                    ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                },
                new TimelineEvent
                {
                    EventType = "EventFlag.Blockbuster2018Phase1",
                    ActiveUntil = Constants.ActiveUntil,
                    ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }
            }
        },
        new SeasonTimelineEvent
        {
            SeasonNumber = 6,
            Events = new List<IEvent>
            {
                new TimelineEvent
                {
                    EventType = "EventFlag.LTM_Fortnitemares",
                    ActiveUntil = Constants.ActiveUntil,
                    ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                },
                new TimelineEvent
                {
                    EventType = "EventFlag.FortnitemaresPhase1",
                    ActiveUntil = Constants.ActiveUntil,
                    ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                },
                new TimelineEvent
                {
                    EventType = "EventFlag.LTM_LilKevin",
                    ActiveUntil = Constants.ActiveUntil,
                    ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                },
                new TimelineEvent
                {
                    EventType = "EventFlag.LobbySeason6Halloween",
                    ActiveUntil = Constants.ActiveUntil,
                    ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }
            }
        },
        new SeasonTimelineEvent
        {
            SeasonNumber = 7,
            Events = new List<IEvent>
            {
                new TimelineEvent
                {
                    EventType = "EventFlag.Frostnite",
                    ActiveUntil = Constants.ActiveUntil,
                    ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                },
                new TimelineEvent
                {
                    EventType = "EventFlag.LTM_14DaysOfFortnite",
                    ActiveUntil = Constants.ActiveUntil,
                    ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                },
                new TimelineEvent
                {
                    EventType = "EventFlag.LTE_Festivus",
                    ActiveUntil = Constants.ActiveUntil,
                    ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                },
                new TimelineEvent
                {
                    EventType = "EventFlag.LTM_WinterDeimos",
                    ActiveUntil = Constants.ActiveUntil,
                    ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }
            }
        },
        new SeasonTimelineEvent
        {
            SeasonNumber = 8,
            Events = new List<IEvent>
            {
                new TimelineEvent { EventType = "EventFlag.Spring2019", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.Spring2019.Phase1", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.LTM_Ashton", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.LTM_Goose", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.LTM_HighStakes", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.LTE_BootyBay", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.Spring2019.Phase2", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") }
            }
        },
        new SeasonTimelineEvent
        {
            SeasonNumber = 10,
            Events = new List<IEvent>
            {
                new TimelineEvent { EventType = "EventFlag.Mayday", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.Season10.Phase2", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.Season10.Phase3", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.LTE_BlackMonday", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.LTE_SharpShooter", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.LTE_Fortnitemares2020", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") }
            }
        },
        new SeasonTimelineEvent
        {
            SeasonNumber = 11,
            Events = new List<IEvent>
            {
                new TimelineEvent { EventType = "EventFlag.Fortnitemares2020", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.Halloween2020", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.LobbyHalloween2020", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.LobbySeason11Halloween", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") }
            }
        },
        new SeasonTimelineEvent
        {
            SeasonNumber = 14,
            Events = new List<IEvent>
            {
                new TimelineEvent { EventType = "EventFlag.Fortnitemares2021", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.LobbyHalloween2021", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") }
            }
        },
        new SeasonTimelineEvent
        {
            SeasonNumber = 15,
            Events = new List<IEvent>
            {
                new TimelineEvent { EventType = "EventFlag.Season15", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.Winterfest2021", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                new TimelineEvent { EventType = "EventFlag.LobbyWinterfest2021", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") }
            }
        },
        new SeasonTimelineEvent
        {
            SeasonNumber = 16,
            Events = new List<IEvent>
            {
                new TimelineEvent { EventType = "EventFlag.Season16", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") }
            }
        },
        new SeasonTimelineEvent
        {
            SeasonNumber = 17,
            Events = new List<IEvent>
            {
                new TimelineEvent { EventType = "EventFlag.Season17", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") }
            }
        },
        new SeasonTimelineEvent
        {
            SeasonNumber = 18,
            Events = new List<IEvent>
            {
                new TimelineEvent { EventType = "EventFlag.Season18", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") }
            }
        },
        new SeasonTimelineEvent
        {
            SeasonNumber = 19,
            Events = new List<IEvent>
            {
                new TimelineEvent { EventType = "EventFlag.Season19", ActiveUntil = Constants.ActiveUntil, ActiveSince = DateTime.MinValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") }
            }
        }
    };
}
