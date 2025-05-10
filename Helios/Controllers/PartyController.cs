using System.Text.Json;
using System.Xml.Linq;
using Helios.Configuration;
using Helios.Database.Repository;
using Helios.Database.Tables.Account;
using Helios.Database.Tables.Fortnite;
using Helios.Database.Tables.XMPP;
using Helios.Socket;
using Helios.Utilities;
using Helios.Utilities.Errors.HeliosErrors;
using Helios.Utilities.Extensions;
using Helios.Utilities.Tokens;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Helios.Controllers;

[ApiController]
[Route("/party/api/v1/")]
public class PartyController : ControllerBase
{
    public Repository<Parties> pRepo = Constants.repositoryPool.For<Parties>();
    
    [HttpGet("Fortnite/user/{accountId}")]
    public async Task<IActionResult> GetUser(string accountId)
    {
        var parties = await pRepo.FindAllByTableAsync();

        var party = parties.FirstOrDefault(p =>
            (p.Members.FromJson<List<PartyMember>>()
                ?.Any(m => m.AccountId == accountId)) == true);

        return Ok(new
        {
            current = party ?? new Parties(),
            pending = Array.Empty<object>(),
            invites = party?.Invites.FromJson<List<PartyInvite>>() ?? new List<PartyInvite>(),
            pings = Array.Empty<object>(), // TODO: I will eventually have to store pings!?!?
        });
    }

    [HttpPost("Fortnite/parties")]
    public async Task<IActionResult> CreateParty()
    {
        string requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
        JObject requestData;

        try
        {
            requestData = JObject.Parse(requestBody);
        }
        catch
        {
            return MCPErrors.InvalidPayload.Apply(HttpContext);
        }

        string timestamp = DateTime.UtcNow.ToIsoUtcString();
        var joinInfo = requestData["join_info"] ?? throw new InvalidOperationException("Missing join_info.");
        var connectionId = joinInfo.SelectToken("connection.id")?.ToString();
        var accountId = connectionId?.Split("@prod")[0] ?? string.Empty;
        var yieldLeadership = joinInfo.SelectToken("connection.yield_leadership")?.Value<bool>() ?? false;
        var joinInfoMeta = joinInfo.SelectToken("meta")?.ToObject<Dictionary<string, object>>();
        var joinInfoConnectionMeta = joinInfo.SelectToken("connection.meta")?.ToObject<Dictionary<string, object>>();

        var partyMemberConnection = new PartyMemberConnection
        {
            Id = connectionId ?? string.Empty,
            ConnectedAt = timestamp,
            UpdatedAt = timestamp,
            YieldLeadership = yieldLeadership,
            Meta = joinInfoConnectionMeta
        };

        var partyMember = new PartyMember
        {
            AccountId = accountId,
            Meta = joinInfoMeta,
            Connections = JsonSerializer.Serialize(new List<PartyMemberConnection> { partyMemberConnection }),
            Revision = 0,
            UpdatedAt = timestamp,
            JoinedAt = timestamp,
            Role = "CAPTAIN"
        };

        string partyId = Guid.NewGuid().ToString().Replace("-", "");
        var newParty = new Parties
        {
            PartyId = partyId,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Config = JsonSerializer.Serialize(requestData["config"].ToObject<Dictionary<string, dynamic>>()),
            Members = JsonSerializer.Serialize(new List<PartyMember> { partyMember }),
            Meta = JsonSerializer.Serialize(requestData["meta"].ToObject<Dictionary<string, string>>()),
            Invites = JsonSerializer.Serialize(new List<PartyInvite>()),
            Applicants = Array.Empty<string>(),
            Revision = 0,
            Intentions = Array.Empty<string>()
        };

        await pRepo.SaveAsync(newParty);

        return Ok(new
        {
            id = partyId,
            created_at = timestamp,
            updated_at = timestamp,
            config = requestData["config"].ToObject<Dictionary<string, dynamic>>(),
            members = new List<object>
            {
                new
                {
                    account_id = accountId,
                    meta = joinInfoMeta,
                    connections = new List<PartyMemberConnection> { partyMemberConnection },
                    revision = 0,
                    updated_at = timestamp,
                    joined_at = timestamp,
                    role = "CAPTAIN"
                }
            },
            meta = requestData["meta"].ToObject<Dictionary<string, string>>(),
            invites = Array.Empty<PartyInvite>(),
            applicants = Array.Empty<string>(),
            revision = 0,
            intentions = Array.Empty<string>()
        });
    }

    [HttpGet("Fortnite/parties/{partyId}")]
    public async Task<IActionResult> GetPartyById(string partyId)
    {
        var party = await pRepo.FindByColumnAsync("partyid", partyId);

        if (party == null)
            return PartyErrors.PartyNotFound.WithMessage($"Party {partyId} does not exist.").Apply(HttpContext);

        var partyConfig = party.Config != null ? JsonSerializer.Deserialize<Dictionary<string, dynamic>>(party.Config) : new Dictionary<string, dynamic>();
        var members = party.Members != null ? JsonSerializer.Deserialize<List<PartyMember>>(party.Members) : new List<PartyMember>();
        var meta = party.Meta != null ? JsonSerializer.Deserialize<Dictionary<string, string>>(party.Meta) : new Dictionary<string, string>();
        var invites = party.Invites != null ? JsonSerializer.Deserialize<List<PartyInvite>>(party.Invites) : new List<PartyInvite>();

        var memberList = members.Select(member => new
        {
            account_id = member.AccountId,
            meta = member.Meta,
            connections = member.Connections != null ? JsonSerializer.Deserialize<List<PartyMemberConnection>>(member.Connections) : new List<PartyMemberConnection>(),
            revision = member.Revision,
            updated_at = member.UpdatedAt,
            joined_at = member.JoinedAt,
            role = member.Role
        }).ToList();

        return Ok(new
        {
            id = party.PartyId,
            created_at = party.CreatedAt,
            updated_at = party.UpdatedAt,
            config = partyConfig,
            members = memberList,
            meta,
            invites,
            applicants = party.Applicants,
            revision = party.Revision,
            intentions = party.Intentions
        });
    }
    
    [HttpPatch("Fortnite/parties/{partyId}")]
    public async Task<IActionResult> ModifyFortniteParty(string partyId)
    {
        if (!await VerifyToken.Verify(HttpContext))
            return AuthenticationErrors.InvalidToken("Failed to verify token.").Apply(HttpContext);
        
        var party = await pRepo.FindByColumnAsync("partyid", partyId);
        if (party == null)
            return PartyErrors.PartyNotFound.Apply(HttpContext);

        string requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
        JObject requestData;

        try
        {
            requestData = JObject.Parse(requestBody);
        }
        catch
        {
            return MCPErrors.InvalidPayload.Apply(HttpContext);
        }

        var configUpdates = requestData["config"]?.ToObject<Dictionary<string, string>>();
        var meta = requestData["meta"];
        var metaDelete = meta?["delete"]?.Values<string>().ToList();
        var metaUpdate = meta?["update"]?.ToObject<Dictionary<string, string>>();

        if (configUpdates?.Count > 0)
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, object>>(party.Config) ?? new();
            foreach (var (key, value) in configUpdates)
                config[key] = value;
            party.Config = JsonSerializer.Serialize(config);
        }

        var partyMeta = JsonSerializer.Deserialize<Dictionary<string, object>>(party.Meta) ?? new();
        bool metaModified = false;

        if (metaDelete?.Count > 0)
        {
            foreach (var key in metaDelete)
                metaModified |= partyMeta.Remove(key);
        }

        if (metaUpdate?.Count > 0)
        {
            foreach (var (key, value) in metaUpdate)
            {
                partyMeta[key] = value;
                metaModified = true;
            }
        }

        if (metaModified)
            party.Meta = JsonSerializer.Serialize(partyMeta);

        party.Revision++;
        party.UpdatedAt = DateTime.UtcNow.ToIsoUtcString();
        await pRepo.UpdateAsync(party);

        var members = JsonSerializer.Deserialize<List<PartyMember>>(party.Members) ?? new();
        var captain = members.FirstOrDefault(x => x.Role == "CAPTAIN");
        var accountIds = members.Select(m => m.AccountId).ToList();

        var sessionsRepo = Constants.repositoryPool.For<ClientSessions>();
        var clients = await sessionsRepo.FindAllByColumnAsync("accountid", accountIds);
        var clientMap = clients
            .GroupBy(c => c.AccountId)
            .ToDictionary(g => g.Key, g => g.First());
        
        var partyConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(party.Config) ?? new();
        object GetSafeValue(string key) => partyConfig.TryGetValue(key, out var val) ? val : null;

        var messageTemplate = new {
            captain_id = captain?.AccountId,
            party_state_updated = metaUpdate,
            party_state_removed = metaDelete,
            party_state_overriden = metaUpdate,
            party_privacy_type = GetSafeValue("joinability"),
            party_type = GetSafeValue("type"),
            party_sub_type = GetSafeValue("sub_type"),
            max_number_of_members = GetSafeValue("max_size"),
            invite_ttl_seconds = GetSafeValue("invite_ttl"),
            intention_ttl_seconds = GetSafeValue("intention_ttl"),
            updated_at = party.UpdatedAt,
            created_at = party.CreatedAt,
            ns = "Fortnite",
            party_id = party.Id,
            sent = DateTime.UtcNow.ToIsoUtcString(),
            revision = party.Revision,
            type = "com.epicgames.social.party.notification.v0.PARTY_UPDATED"
        };

        foreach (var member in members)
        {
            if (!clientMap.TryGetValue(member.AccountId, out var client)) continue;
            if (!Globals._socketConnections.TryGetValue(client.SocketId, out var socket)) continue;

            var xmlMessage = new XElement(XNamespace.Get("jabber:client") + "message",
                new XAttribute("to", client.Jid),
                new XAttribute("from", "xmpp-admin@prod.ol.epicgames.com"),
                new XElement("body", JsonSerializer.Serialize(messageTemplate))
            );

            socket.Send(xmlMessage.ToString(SaveOptions.DisableFormatting));
        }

        return Ok();
    }

    [HttpPatch("Fortnite/parties/{partyId}/members/{accountId}/meta")]
    public async Task<IActionResult> ModifyFortnitePartyMeta(string partyId, string accountId)
    {
       var party = await pRepo.FindByColumnAsync("partyid", partyId);

        if (party == null)
            return PartyErrors.PartyNotFound.WithMessage($"Party {partyId} does not exist.").Apply(HttpContext);

        string requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
        JObject requestData;

        try
        {
            requestData = JObject.Parse(requestBody);
        }
        catch
        {
            return MCPErrors.InvalidPayload.Apply(HttpContext);
        }

        var metaDelete = requestData.SelectToken("delete") as JArray;
        var metaUpdate = requestData.SelectToken("update")?.ToObject<Dictionary<string, string>>();

        var deserializedPartyMembers = JsonSerializer.Deserialize<List<PartyMember>>(party.Members);

        var member = deserializedPartyMembers.FirstOrDefault(m => m.AccountId == accountId);
        if (member == null)
            return PartyErrors.MemberNotFound.WithMessage($"Member {accountId} not found in party {partyId}.").Apply(HttpContext);

        int memberIndex = deserializedPartyMembers.IndexOf(member);

        var partyMeta = JsonSerializer.Deserialize<Dictionary<string, object>>(party.Meta) ?? new();
        bool metaModified = false;

        if (metaDelete?.Count > 0)
        {
            foreach (var token in metaDelete)
            {
                string key = token.ToString(); 
                metaModified |= partyMeta.Remove(key);
            }
        }
        
        if (metaUpdate?.Count > 0)
        {
            foreach (var (key, value) in metaUpdate)
            {
                partyMeta[key] = value;
                metaModified = true;
            }
        }

        if (metaModified)
            party.Meta = JsonSerializer.Serialize(partyMeta);

        var currentTime = DateTime.UtcNow;
        string isoTime = currentTime.ToIsoUtcString();

        member.UpdatedAt = isoTime;
        deserializedPartyMembers[memberIndex] = member;
        party.UpdatedAt = isoTime;

        await pRepo.UpdateAsync(party);

        var messageBody = new
        {
            account_id = accountId,
            account_dn = member.Meta["urn:epic:member:dn_s"],
            member_state_updated = metaUpdate ?? new Dictionary<string, string>(),
            member_state_removed = metaDelete ?? new JArray(),
            member_state_overridden = new { },
            party_id = party.PartyId,
            updated_at = currentTime.ToIsoUtcString(),
            sent = currentTime.ToIsoUtcString(),
            revision = member.Revision,
            ns = "Fortnite",
            type = "com.epicgames.social.party.notification.v0.MEMBER_STATE_UPDATED"
        };

        string jsonBody = JsonConvert.SerializeObject(messageBody);

        var sessionsRepo = Constants.repositoryPool.For<ClientSessions>();
        var accountIds = deserializedPartyMembers.Select(m => m.AccountId).ToList();
        
        var clients = await sessionsRepo.FindAllByColumnAsync("accountid", accountIds);
        var clientMap = clients
            .GroupBy(c => c.AccountId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var partyMember in deserializedPartyMembers)
        {
            if (!clientMap.TryGetValue(partyMember.AccountId, out var client)) continue;
            if (!Globals._socketConnections.TryGetValue(client.SocketId, out var clientSocket)) continue;
            
            var xmlMessage = new XElement(XNamespace.Get("jabber:client") + "message",
                new XAttribute("xmlns", "jabber:client"),
                new XAttribute("to", client.Jid),
                new XAttribute("from", "xmpp-admin@prod.ol.epicgames.com"),
                new XElement("body", jsonBody)
            );
            
            string xmlString = xmlMessage.ToString(SaveOptions.DisableFormatting);
            clientSocket.Send(xmlString);
        }

        return Ok();
    }
}