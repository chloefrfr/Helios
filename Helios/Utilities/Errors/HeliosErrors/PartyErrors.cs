namespace Helios.Utilities.Errors.HeliosErrors;

public class PartyErrors
{
    private const string Service = "com.helios.party";
    
    public static ApiError PartyNotFound => new(
        "errors.com.epicgames.party.partyNotFound",
        "Party {0} does not exist.", Service, 51002, 400);
    
    public static ApiError MemberNotFound => new(
        "errors.com.epicgames.party.memberNotFound",
        "Party member {0} does not exist.", Service, 51004, 400);
}