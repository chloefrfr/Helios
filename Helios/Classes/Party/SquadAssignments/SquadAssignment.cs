using Newtonsoft.Json;

namespace Helios.Classes.Party.SquadAssignments;

public class SquadAssignment
{
    [JsonProperty("memberId")] 
    public string MemberId { get; set; } = string.Empty;
    [JsonProperty("absoluteMemberIdx")]
    public int AbsoluteMemberIdx { get; set; }
}