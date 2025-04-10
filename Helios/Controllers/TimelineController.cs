using Helios.Managers;
using Helios.Utilities;
using Helios.Utilities.Errors.HeliosErrors;
using Helios.Utilities.Extensions;
using Helios.Utilities.Timeline;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Controllers;

[ApiController]
[Route("/fortnite/api/calendar/v1/timeline")]
public class TimelineController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCalendar([FromHeader(Name = "User-Agent")] string userAgent)
    {
        string nextDayMidnight = DateTime.UtcNow.AddDays(1).ToIsoUtcString();

        if (string.IsNullOrEmpty(userAgent))
        {
            return InternalErrors.InvalidUserAgent.Apply(HttpContext);
        }

        var uahelper = UserAgentParser.Parse(userAgent);
        if (uahelper == null)
        {
            return InternalErrors.InvalidUserAgent.Apply(HttpContext);
        }

        var activeEvents = await TimelineManager.CreateTimelineAsync(userAgent);

        var channels = new Channels
        {
            clientMatchmaking = new ClientMatchmaking
            {
                states = new List<object>(),
                cacheExpire = nextDayMidnight
            },
            communityVotes = new CommunityVotes
            {
                states = new List<VoteState>
                {
                    new VoteState
                    {
                        validFrom = DateTime.MinValue.ToIsoUtcString(),
                        activeEvents = new List<string>(),
                        state = new ElectionState
                        {
                            electionId = string.Empty,
                            candidates = new List<string>(),
                            electionEnds = nextDayMidnight,
                            numWinners = 1,
                            wipeNumber = 1,
                            winnerStateHours = 1,
                            offers = new List<string>()
                        }
                    }
                },
                cacheExpire = nextDayMidnight
            },
            clientEvents = new ClientEvents
            {
                states = new List<EventState>
                {
                    new EventState
                    {
                        validFrom = DateTime.MinValue.ToIsoUtcString(),
                        activeEvents = activeEvents,
                        state = new EventInfo
                        {
                            activeStorefronts = new List<string>(),
                            eventNamedWeights = new Dictionary<string, object>(),
                            seasonNumber = uahelper.Season,
                            seasonTemplateId = $"AthenaSeason:athenaseason{uahelper.Season}",
                            matchXpBonusPoints = 0,
                            seasonBegin = DateTime.Parse("2020-01-01T00:00:00.000Z").ToIsoUtcString(),
                            seasonEnd = DateTime.Parse("9999-01-01T00:00:00.000Z").ToIsoUtcString(),
                            seasonDisplayedEnd = DateTime.Parse("9999-01-01T00:00:00.000Z").ToIsoUtcString(),
                            weeklyStoreEnd = nextDayMidnight,
                            stwEventStoreEnd = nextDayMidnight,
                            stwWeeklyStoreEnd = nextDayMidnight,
                            sectionStoreEnds = new Dictionary<string, string>(),
                            dailyStoreEnd = nextDayMidnight
                        }
                    }
                },
                cacheExpire = nextDayMidnight
            }
        };

        var response = new
        {
            channels,
            eventsTimeOffsetHrs = 0,
            cacheIntervalMins = 10,
            currentTime = DateTime.UtcNow.ToIsoUtcString()
        };

        return Ok(response);
    }
}