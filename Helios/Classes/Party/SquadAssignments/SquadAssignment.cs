using Newtonsoft.Json;

namespace Helios.Classes.Party.SquadAssignments;

public class SquadAssignment
{
    public string memberId { get; set; } = string.Empty;
    public int absoluteMemberIdx { get; set; }
}