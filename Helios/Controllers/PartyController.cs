using System.Text.Json;
using System.Xml.Linq;
using Helios.Classes.Party.SquadAssignments;
using Helios.Configuration;
using Helios.Database.Repository;
using Helios.Database.Tables.Account;
using Helios.Database.Tables.Party;
using Helios.Database.Tables.XMPP;
using Helios.Managers;
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
    private Repository<Parties> pRepo = Constants.repositoryPool.For<Parties>(false);
    private Repository<Invites> iRepo = Constants.repositoryPool.For<Invites>(false);
    private Repository<Friends> fRepo = Constants.repositoryPool.For<Friends>(false);
    private Repository<Pings> pingsRepo = Constants.repositoryPool.For<Pings>(false);
    private Repository<User> uRepo = Constants.repositoryPool.For<User>(false);

    [HttpGet("Fortnite/user/{accountId}")]
    public async Task<IActionResult> GetUser(string accountId)
    {
        var parties = await pRepo.FindAllByTableAsync();

        var currentParties = parties
            .Where(p =>
                p.Members.FromJson<List<PartyMember>>()?
                    .Any(m => m.AccountId == accountId) == true)
            .ToList();

        var invites = await iRepo.FindAllAsync(new Invites { SentBy = accountId });
        var pings = await pingsRepo.FindAllAsync(new Pings { SentBy = accountId });

        var formattedInvites = invites.Select(x => new
        {
            party_id = x.PartyId,
            meta = JsonSerializer.Deserialize<Dictionary<string, string>>(x.Meta),
            sent_by = x.SentBy,
            sent_to = x.SentTo,
            sent_at = x.SentAt,
            updated_at = x.UpdatedAt,
            expires_at = x.ExpiresAt,
            status = x.Status,
        });

        var formattedPings = pings.Select(x => new
        {
            sent_by = x.SentBy,
            sent_to = x.SentTo,
            sent_at = x.SentAt,
            meta = JsonSerializer.Deserialize<Dictionary<string, string>>(x.Meta),
            expires_at = x.ExpiresAt
        });

        return Ok(new
        {
            current = currentParties,
            pending = Array.Empty<object>(),
            invites = formattedInvites,
            pings = formattedPings
        });
    }

    [HttpPost("Fortnite/parties")]
    public async Task<IActionResult> CreateOrUpdateParty()
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
        var joinInfoMeta = joinInfo.SelectToken("meta") is JToken metaToken
            ? PartyManager.ConvertJTokenToObject(metaToken)
            : joinInfo.SelectToken("meta")?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();

        var joinInfoConnectionMeta = joinInfo.SelectToken("connection.meta") is JToken connMetaToken
            ? PartyManager.ConvertJTokenToObject(connMetaToken)
            : joinInfo.SelectToken("connection.meta")?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();

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
            invites = Array.Empty<Invites>(),
            applicants = Array.Empty<string>(),
            revision = 0,
            intentions = Array.Empty<string>()
        });
    }

    [HttpGet("Fortnite/parties/{partyId}")]
    public async Task<IActionResult> GetPartyById(string partyId)
    {
        var party = await pRepo.FindByColumnAsync("partyid", partyId, useCache: false);

        if (party == null)
        {
            Logger.Error($"Party {partyId} not found.");
            return PartyErrors.PartyNotFound.WithMessage($"Party {partyId} does not exist.").Apply(HttpContext);
        }

        var partyConfig = party.Config != null ? JsonSerializer.Deserialize<Dictionary<string, dynamic>>(party.Config) : new Dictionary<string, dynamic>();
        var members = party.Members != null ? JsonSerializer.Deserialize<List<PartyMember>>(party.Members) : new List<PartyMember>();
        var meta = party.Meta != null ? JsonSerializer.Deserialize<Dictionary<string, string>>(party.Meta) : new Dictionary<string, string>();

        var memberAccountIds = members.Select(m => m.AccountId).ToList();
        var invites = await iRepo.FindAllByColumnAsync("sent_by", memberAccountIds, useCache: false);

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
        var party = await pRepo.FindByColumnAsync("partyid", partyId);
        if (party == null)
            return PartyErrors.PartyNotFound.Apply(HttpContext);

        JObject requestData;
        try
        {
            requestData = JObject.Parse(await new StreamReader(Request.Body).ReadToEndAsync());
        }
        catch
        {
            return MCPErrors.InvalidPayload.Apply(HttpContext);
        }

        var partyConfig = JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(party.Config) ?? new();
        var config = requestData["config"]?.ToObject<Dictionary<string, object>>();
        if (config != null)
        {
            foreach (var (key, value) in config)
            {
                partyConfig[key] = JsonSerializer.SerializeToElement(value);
            }
        }

        var partyMeta = JsonSerializer.Deserialize<Dictionary<string, object>>(party.Meta) ?? new();
        var meta = requestData["meta"];

        var metaDelete = meta?["delete"]?.Values<string>().ToList();
        if (metaDelete?.Any() == true)
        {
            foreach (var key in metaDelete)
            {
                partyMeta.Remove(key);
            }
        }

        var metaUpdate = meta?["update"]?.ToObject<Dictionary<string, string>>();
        if (metaUpdate?.Any() == true)
        {
            foreach (var (key, value) in metaUpdate)
            {
                if (key.EndsWith("Time_s") && DateTime.TryParse(value, out DateTime parsedTime))
                {
                    partyMeta[key] = parsedTime.ToIsoUtcString();
                }
                else
                {
                    partyMeta[key] = value;
                }
            }
        }

        party.Meta = JsonSerializer.Serialize(partyMeta);
        party.Config = JsonSerializer.Serialize(partyConfig);
        party.Revision++;
        party.UpdatedAt = DateTime.UtcNow.ToIsoUtcString();

        await pRepo.UpdateAsync(party);

        var members = JsonSerializer.Deserialize<List<PartyMember>>(party.Members) ?? new();
        var captain = members.FirstOrDefault(x => x.Role == "CAPTAIN");
        var accountIds = members.Select(m => m.AccountId).ToList();

        var sessionsRepo = Constants.repositoryPool.For<ClientSessions>();
        var clients = await sessionsRepo.FindAllByColumnAsync("accountid", accountIds);
        var clientMap = clients.ToDictionary(c => c.AccountId, c => c);

        var messageTemplate = new
        {
            captain_id = captain?.AccountId,
            party_state_updated = metaUpdate,
            party_state_removed = metaDelete,
            party_privacy_type = partyConfig.TryGetValue("joinability", out var j) ? j.GetString() : null,
            party_type = partyConfig.TryGetValue("type", out var t) ? t.GetString() : "DEFAULT",
            party_sub_type = partyMeta.TryGetValue("urn:epic:cfg:party-type-id_s", out var st) ? st?.ToString() : null,
            max_number_of_members = partyConfig.TryGetValue("max_size", out var ms) ? ms.GetInt32() : 16,
            invite_ttl_seconds = partyConfig.TryGetValue("invite_ttl", out var ttl) ? ttl.GetInt32() : 14400,
            updated_at = party.UpdatedAt,
            created_at = party.CreatedAt,
            ns = "Fortnite",
            party_id = party.PartyId,
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
                new XElement("body", JsonConvert.SerializeObject(messageTemplate))
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

        var metaDelete = requestData["delete"]?.ToObject<List<string>>() ?? new List<string>();
        var metaUpdate = requestData["update"]?.ToObject<Dictionary<string, object>>();

        var partyMembers = JsonSerializer.Deserialize<List<PartyMember>>(party.Members) ?? new List<PartyMember>();
        var member = partyMembers.FirstOrDefault(m => m.AccountId == accountId);
        if (member == null)
            return PartyErrors.MemberNotFound.WithMessage($"Member {accountId} not found in party {partyId}.").Apply(HttpContext);

        var currentTime = DateTime.UtcNow;
        string isoTime = currentTime.ToIsoUtcString();

        member.Meta ??= new Dictionary<string, object>();

        foreach (var key in metaDelete)
        {
            member.Meta.Remove(key);
        }

        if (metaUpdate != null)
        {
            foreach (var (key, value) in metaUpdate)
            {
                member.Meta[key] = value;
            }
        }

        member.UpdatedAt = isoTime;
        member.Revision++;

        party.Members = JsonSerializer.Serialize(partyMembers);
        party.UpdatedAt = isoTime;
        party.Revision++;
        await pRepo.UpdateAsync(party);

        var memberDn = member.Meta.TryGetValue("urn:epic:member:dn_s", out var dn) ? dn?.ToString() : null;
        var messageBody = new
        {
            account_id = accountId,
            account_dn = memberDn,
            member_state_updated = metaUpdate ?? new Dictionary<string, object>(),
            member_state_removed = metaDelete,
            member_state_overridden = new { },
            party_id = party.PartyId,
            updated_at = isoTime,
            sent = isoTime,
            revision = member.Revision,
            ns = "Fortnite",
            type = "com.epicgames.social.party.notification.v0.MEMBER_STATE_UPDATED"
        };

        string jsonBody = JsonConvert.SerializeObject(messageBody);
        var accountIds = partyMembers.Select(m => m.AccountId).ToList();
        var clients = await Constants.repositoryPool.For<ClientSessions>()
            .FindAllByColumnAsync("accountid", accountIds);

        var xmlBase = new XElement(XNamespace.Get("jabber:client") + "message",
            new XAttribute("xmlns", "jabber:client"),
            new XAttribute("from", "xmpp-admin@prod.ol.epicgames.com"),
            new XElement("body", jsonBody)
        );

        var socketMap = clients
            .Where(c => Globals._socketConnections.ContainsKey(c.SocketId))
            .ToDictionary(
                c => c.AccountId,
                c => new { Client = c, Socket = Globals._socketConnections[c.SocketId] }
            );

        Parallel.ForEach(partyMembers, partyMember =>
        {
            if (!socketMap.TryGetValue(partyMember.AccountId, out var clientInfo))
                return;

            var xmlMessage = new XElement(xmlBase);
            xmlMessage.SetAttributeValue("to", clientInfo.Client.Jid);

            string xmlString = xmlMessage.ToString(SaveOptions.DisableFormatting);
            clientInfo.Socket.Send(xmlString);
        });

        return Ok();
    }

    [HttpPost("Fortnite/parties/{partyId}/members/{accountId}/join")]
    public async Task<IActionResult> JoinFortniteParty(string partyId, string accountId)
    {
        var party = await pRepo.FindByColumnAsync("partyid", partyId);
        if (party == null)
        {
            Logger.Error($"Party {partyId} does not exist.");
            return PartyErrors.PartyNotFound.WithMessage($"Party {partyId} does not exist.").Apply(HttpContext);
        }

        string requestBody;
        using (var reader = new StreamReader(Request.Body))
        {
            requestBody = await reader.ReadToEndAsync();
        }

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

        var connectionInfo = requestData["connection"];
        if (connectionInfo == null)
            return MCPErrors.InvalidPayload.WithMessage("Missing connection object.").Apply(HttpContext);

        string connectionId = connectionInfo["id"]?.ToString();
        string memberId = connectionId?.Split("@prod")[0] ?? accountId;

        var memberMeta = requestData["meta"] is JToken token1
            ? PartyManager.ConvertJTokenToObject(token1)
            : new Dictionary<string, object>();

        var memberConnectionMeta = connectionInfo["meta"] is JToken token2
            ? PartyManager.ConvertJTokenToObject(token2)
            : new Dictionary<string, object>();

        var partyMemberConnection = new PartyMemberConnection
        {
            Id = connectionId,
            ConnectedAt = timestamp,
            UpdatedAt = timestamp,
            YieldLeadership = false,
            Meta = memberConnectionMeta
        };

        var deserializedPartyMembers = JsonSerializer.Deserialize<List<PartyMember>>(party.Members ?? "[]") ?? new();
        deserializedPartyMembers = deserializedPartyMembers.Where(member => member.AccountId != accountId).ToList();

        deserializedPartyMembers.Add(new PartyMember
        {
            AccountId = memberId,
            Meta = memberMeta,
            Connections = JsonSerializer.Serialize(new List<PartyMemberConnection> { partyMemberConnection }),
            Revision = 0,
            UpdatedAt = timestamp,
            JoinedAt = timestamp,
            Role = "MEMBER"
        });

        Console.WriteLine(JsonConvert.SerializeObject(deserializedPartyMembers, Formatting.Indented));

        var partyMeta = party.Meta != null
            ? JsonConvert.DeserializeObject<Dictionary<string, object>>(party.Meta)
            : new Dictionary<string, object>();

        string squadAssignmentsKey = partyMeta.ContainsKey("Default:RawSquadAssignments_j")
            ? "Default:RawSquadAssignments_j"
            : "RawSquadAssignments_j";

        string rawJson = partyMeta.TryGetValue(squadAssignmentsKey, out var rawObj) && rawObj != null
            ? rawObj.ToString()
            : "{\"RawSquadAssignments\":[]}";

        var squadAssignments = JsonConvert.DeserializeObject<SquadAssignmentsWrapper>(rawJson)
                               ?? new SquadAssignmentsWrapper();

        if (!squadAssignments.RawSquadAssignments.Any(sa => sa.memberId == memberId))
        {
            squadAssignments.RawSquadAssignments.Add(new SquadAssignment
            {
                memberId = memberId,
                absoluteMemberIdx = deserializedPartyMembers.Count - 1
            });
        }

        var partyMember = deserializedPartyMembers.FirstOrDefault(m => m.AccountId == accountId);
        if (partyMember == null)
            return PartyErrors.MemberNotFound.WithMessage($"Member {accountId} not found in party {partyId}.").Apply(HttpContext);

        partyMember.Revision++;

        partyMeta[squadAssignmentsKey] = JsonSerializer.Serialize(squadAssignments);
        party.Meta = JsonSerializer.Serialize(partyMeta);

        party.Members = JsonSerializer.Serialize(deserializedPartyMembers);
        party.Meta = JsonSerializer.Serialize(partyMeta);

        party.Revision++;
        party.UpdatedAt = timestamp;

        await pRepo.UpdateAsync(party);

        var partyCaptain = deserializedPartyMembers.FirstOrDefault(x => x.Role == "CAPTAIN");
        var partyConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(party.Config ?? "{}") ?? new();
        string privacyType = partyConfig.TryGetValue("joinability", out var joinabilityObj)
            ? joinabilityObj?.ToString() ?? "PUBLIC"
            : "PUBLIC";

        partyMeta.TryGetValue("urn:epic:cfg:party-type-id_s", out var subTypeObj);
        string partySubType = subTypeObj?.ToString();

        var memberJoinedMessage = new
        {
            account_id = memberId,
            account_dn = memberMeta.GetValueOrDefault("urn:epic:member:dn_s", ""),
            connection = new
            {
                connected_at = timestamp,
                id = connectionId,
                meta = memberConnectionMeta,
                updated_at = timestamp
            },
            joined_at = timestamp,
            member_state_updated = memberMeta,
            ns = "Fortnite",
            party_id = party.PartyId,
            revision = 0,
            sent = timestamp,
            type = "com.epicgames.social.party.notification.v0.MEMBER_JOINED",
            updated_at = timestamp,
        };

        var partyUpdatedMessage = new
        {
            captain_id = partyCaptain?.AccountId,
            created_at = party.CreatedAt,
            invite_ttl_seconds = 14400,
            max_number_of_members = 16,
            ns = "Fortnite",
            party_id = party.PartyId,
            party_privacy_type = privacyType,
            party_state_overriden = new object(),
            party_state_removed = Array.Empty<string>(),
            party_state_updated = new Dictionary<string, object>
            {
                { squadAssignmentsKey, JsonConvert.SerializeObject(squadAssignments) }
            },
            party_sub_type = partySubType,
            party_type = "DEFAULT",
            revision = party.Revision,
            sent = timestamp,
            type = "com.epicgames.social.party.notification.v0.PARTY_UPDATED",
            updated_at = timestamp,
        };

        string jsonMemberJoined = JsonConvert.SerializeObject(memberJoinedMessage, Formatting.Indented);
        string jsonPartyUpdated = JsonConvert.SerializeObject(partyUpdatedMessage, Formatting.Indented);

        var sessionsRepo = Constants.repositoryPool.For<ClientSessions>();
        var accountIds = deserializedPartyMembers.Select(m => m.AccountId).ToList();
        var clients = await sessionsRepo.FindAllByColumnAsync("accountid", accountIds);

        var clientMap = clients.GroupBy(c => c.AccountId)
            .ToDictionary(g => g.Key, g => g.FirstOrDefault());

        foreach (var member in deserializedPartyMembers)
        {
            if (!clientMap.TryGetValue(member.AccountId, out var client) ||
                !Globals._socketConnections.TryGetValue(client.SocketId, out var clientSocket))
                continue;

            var xmlMemberJoined = PartyManager.CreateXmlMessage(client.Jid, jsonMemberJoined);
            var xmlPartyUpdated = PartyManager.CreateXmlMessage(client.Jid, jsonPartyUpdated);

            clientSocket.Send(xmlMemberJoined);
            clientSocket.Send(xmlPartyUpdated);
        }

        return Ok(new
        {
            status = "JOINED",
            party_id = party.PartyId
        });
    }

    [HttpDelete("Fortnite/parties/{partyId}/members/{accountId}")]
    public async Task<IActionResult> RemovePartyMember(string partyId, string accountId)
    {
        var party = await pRepo.FindByColumnAsync("partyid", partyId);
        if (party == null)
        {
            Logger.Error($"Party {partyId} does not exist.");
            return PartyErrors.PartyNotFound.WithMessage($"Party {partyId} does not exist.").Apply(HttpContext);
        }

        string timestamp = DateTime.UtcNow.ToIsoUtcString();
        var partyMembers = JsonSerializer.Deserialize<List<PartyMember>>(party.Members);

        int memberIndex = partyMembers.FindIndex(x => x.AccountId == accountId);
        if (memberIndex == -1)
            return PartyErrors.MemberNotFound.WithMessage($"Member {accountId} not found in party {partyId}.")
                .Apply(HttpContext);

        partyMembers.RemoveAt(memberIndex);
        party.Revision++;
        party.UpdatedAt = timestamp;
        party.Members = JsonSerializer.Serialize(partyMembers);

        var memberLeftBody = JsonConvert.SerializeObject(new
        {
            account_id = accountId,
            member_state_update = new { },
            party_id = party.PartyId,
            sent = timestamp,
            revision = party.Revision,
            ns = "Fortnite",
            type = "com.epicgames.social.party.notification.v0.MEMBER_LEFT"
        });

        var baseXml = new XElement(XNamespace.Get("jabber:client") + "message",
            new XAttribute("xmlns", "jabber:client"),
            new XAttribute("from", "xmpp-admin@prod.ol.epicgames.com"),
            new XElement("body", memberLeftBody)
        );

        if (partyMembers.Count > 0)
        {
            var accountIds = partyMembers.Select(m => m.AccountId).ToList();
            var clients = await Constants.repositoryPool.For<ClientSessions>()
                .FindAllByColumnAsync("accountid", accountIds);

            var socketMap = clients
                .Where(c => Globals._socketConnections.ContainsKey(c.SocketId))
                .ToDictionary(
                    c => c.AccountId,
                    c => new { Client = c, Socket = Globals._socketConnections[c.SocketId] }
                );

            foreach (var member in partyMembers)
            {
                if (!socketMap.TryGetValue(member.AccountId, out var clientInfo))
                    continue;

                var xml = new XElement(baseXml);
                xml.SetAttributeValue("to", clientInfo.Client.Jid);
                clientInfo.Socket.Send(xml.ToString(SaveOptions.DisableFormatting));
            }

            await pRepo.UpdateAsync(party);

            var partyMeta = JsonSerializer.Deserialize<Dictionary<string, object>>(party.Meta) ?? new();

            string assignmentKey;
            if (partyMeta.ContainsKey("Default:RawSquadAssignments_j"))
                assignmentKey = "Default:RawSquadAssignments_j";
            else
                assignmentKey = "RawSquadAssignments_j";

            if (partyMeta.ContainsKey(assignmentKey))
            {
                var jsonString = partyMeta[assignmentKey]?.ToString();
                if (!string.IsNullOrEmpty(jsonString))
                {
                    var rawSquadAssignment = JsonConvert.DeserializeObject<SquadAssignmentsWrapper>(jsonString);
                    var index = rawSquadAssignment.RawSquadAssignments.FindIndex(x => x.memberId == accountId);

                    if (index != -1)
                    {
                        rawSquadAssignment.RawSquadAssignments.RemoveAt(index);
                        partyMeta[assignmentKey] = JsonConvert.SerializeObject(rawSquadAssignment);
                    }

                    var captain = partyMembers.FirstOrDefault(x => x.Role == "CAPTAIN");
                    if (captain == null && partyMembers.Count > 0)
                    {
                        partyMembers[0].Role = "CAPTAIN";
                        captain = partyMembers[0];

                        var messageBody = new
                        {
                            account_id = captain.AccountId,
                            member_state_update = new { },
                            ns = "Fortnite",
                            party_id = party.PartyId,
                            revision = party.Revision,
                            sent = timestamp,
                            type = "com.epicgames.social.party.notification.v0.MEMBER_NEW_CAPTAIN"
                        };

                        var captainMessageBody = JsonConvert.SerializeObject(messageBody, Formatting.Indented);

                        foreach (var member in partyMembers)
                        {
                            if (!socketMap.TryGetValue(member.AccountId, out var clientInfo))
                                continue;

                            var captainMessageXml = new XElement(XNamespace.Get("jabber:client") + "message",
                                new XAttribute("xmlns", "jabber:client"),
                                new XAttribute("from", "xmpp-admin@prod.ol.epicgames.com"),
                                new XAttribute("to", clientInfo.Client.Jid),
                                new XElement("body", captainMessageBody)
                            );

                            clientInfo.Socket.Send(captainMessageXml.ToString(SaveOptions.DisableFormatting));
                        }
                    }

                    party.UpdatedAt = timestamp;
                    party.Meta = JsonSerializer.Serialize(partyMeta);
                    party.Members = JsonSerializer.Serialize(partyMembers);

                    await pRepo.UpdateAsync(party);

                    var partyConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(party.Config) ??
                                      new();
                    string privacyType = "PUBLIC";
                    if (partyConfig.ContainsKey("joinability"))
                        privacyType = partyConfig["joinability"].ToString();

                    string partySubType = null;
                    if (partyMeta.ContainsKey("urn:epic:cfg:party-type-id_s"))
                        partySubType = partyMeta["urn:epic:cfg:party-type-id_s"].ToString();

                    var updateBody = JsonConvert.SerializeObject(new
                    {
                        captain_id = captain?.AccountId,
                        created_at = party.CreatedAt,
                        invite_ttl_seconds = 14400,
                        max_number_of_members = partyConfig.TryGetValue("max_size", out var ms) ? ms.GetInt32() : 16,
                        ns = "Fortnite",
                        party_id = party.PartyId,
                        party_privacy_type = privacyType,
                        party_state_overriden = new Dictionary<string, object>(),
                        party_state_removed = new List<string>(),
                        party_state_updated = new Dictionary<string, object>
                        {
                            { assignmentKey, JsonConvert.SerializeObject(rawSquadAssignment) }
                        },
                        party_sub_type = partySubType,
                        party_type = "DEFAULT",
                        revision = party.Revision,
                        sent = timestamp,
                        type = "com.epicgames.social.party.notification.v0.PARTY_UPDATED",
                        updated_at = party.UpdatedAt
                    }, Formatting.Indented);

                    foreach (var member in partyMembers)
                    {
                        if (!socketMap.TryGetValue(member.AccountId, out var clientInfo))
                            continue;

                        var messageXml = new XElement(XNamespace.Get("jabber:client") + "message",
                            new XAttribute("xmlns", "jabber:client"),
                            new XAttribute("from", "xmpp-admin@prod.ol.epicgames.com"),
                            new XAttribute("to", clientInfo.Client.Jid),
                            new XElement("body", updateBody)
                        );

                        clientInfo.Socket.Send(messageXml.ToString(SaveOptions.DisableFormatting));
                    }
                }
            }
        }
        else
        {
            await pRepo.DeleteByColumnAsync("partyid", party.PartyId);
        }

        return Ok();
    }

    [HttpPost("Fortnite/parties/{partyId}/members/{accountId}/promote")]
    public async Task<IActionResult> PromotePartyMember(string partyId, string accountId)
    {
        var party = await pRepo.FindByColumnAsync("partyid", partyId);
        if (party == null)
            return PartyErrors.PartyNotFound.WithMessage($"Party {partyId} does not exist.").Apply(HttpContext);

        var partyMembers = JsonSerializer.Deserialize<List<PartyMember>>(party.Members);
        int newCaptainIndex = partyMembers.FindIndex(x => x.AccountId == accountId);

        if (newCaptainIndex == -1)
            return PartyErrors.MemberNotFound.WithMessage($"Member {accountId} not found in party {partyId}.")
                .Apply(HttpContext);

        int currentCaptainIndex = partyMembers.FindIndex(x => x.Role == "CAPTAIN");
        if (currentCaptainIndex != -1)
            partyMembers[currentCaptainIndex].Role = "MEMBER";

        partyMembers[newCaptainIndex].Role = "CAPTAIN";

        string timestamp = DateTime.Now.ToIsoUtcString();
        party.Members = JsonSerializer.Serialize(partyMembers);
        party.UpdatedAt = timestamp;
        party.Revision++;
        await pRepo.UpdateAsync(party);

        var messageBody = new
        {
            account_id = accountId,
            member_state_update = new { },
            ns = "Fortnite",
            party_id = party.PartyId,
            revision = party.Revision,
            sent = timestamp,
            type = "com.epicgames.social.party.notification.v0.MEMBER_NEW_CAPTAIN"
        };
        string jsonBody = JsonConvert.SerializeObject(messageBody);

        var accountIds = partyMembers.Select(m => m.AccountId).ToList();
        var clients = await Constants.repositoryPool.For<ClientSessions>()
            .FindAllByColumnAsync("accountid", accountIds);

        var xmlns = XNamespace.Get("jabber:client");
        var xmlBase = new XElement(xmlns + "message",
            new XAttribute("xmlns", "jabber:client"),
            new XAttribute("from", "xmpp-admin@prod.ol.epicgames.com"),
            new XElement("body", jsonBody)
        );

        foreach (var client in clients)
        {
            if (!Globals._socketConnections.TryGetValue(client.SocketId, out var socket))
                continue;

            var xmlMessage = new XElement(xmlBase);
            xmlMessage.SetAttributeValue("to", client.Jid);

            socket.Send(xmlMessage.ToString(SaveOptions.DisableFormatting));
        }

        return Ok();
    }

    [HttpPost("Fortnite/parties/{partyId}/invites/{accountId}")]
    public async Task<IActionResult> InviteMemberToParty(string partyId, string accountId)
    {
        if (!await VerifyToken.Verify(HttpContext))
            return AuthenticationErrors.InvalidToken("Failed to verify token.").Apply(HttpContext);

        var party = await pRepo.FindByColumnAsync("partyid", partyId);
        if (party == null)
            return PartyErrors.PartyNotFound.WithMessage($"Party {partyId} does not exist.").Apply(HttpContext);

        Dictionary<string, object> meta;
        try
        {
            using var reader = new StreamReader(Request.Body);
            var requestBody = await reader.ReadToEndAsync();
            var requestData = JObject.Parse(requestBody);
            meta = requestData is JToken token ? PartyManager.ConvertJTokenToObject(token) : new Dictionary<string, object>();
        }
        catch
        {
            return MCPErrors.InvalidPayload.Apply(HttpContext);
        }

        if (!HttpContext.Request.Cookies.TryGetValue("User", out var userJson) || string.IsNullOrEmpty(userJson))
            return AccountErrors.AccountNotFound(HttpContext.Request.Cookies["AccountId"] ?? "unknown").Apply(HttpContext);

        var user = JsonSerializer.Deserialize<User>(userJson);
        var timestamp = DateTime.UtcNow.ToIsoUtcString();
        var expiryTime = DateTime.Parse(timestamp).AddHours(1).ToIsoUtcString();

        var newInvite = new Invites
        {
            PartyId = party.PartyId,
            SentBy = user.AccountId,
            Meta = JsonSerializer.Serialize(meta),
            SentTo = accountId,
            SentAt = timestamp,
            UpdatedAt = timestamp,
            ExpiresAt = expiryTime,
            Status = "SENT"
        };

        await iRepo.SaveAsync(newInvite);

        party.UpdatedAt = timestamp;
        await pRepo.UpdateAsync(party);

        var partyMembers = JsonSerializer.Deserialize<List<PartyMember>>(party.Members);
        var inviter = partyMembers.FirstOrDefault(x => x.AccountId == user.AccountId);
        if (inviter == null)
            return AccountErrors.AccountNotFound(user.AccountId).WithMessage($"Inviter {user.AccountId} not found.").Apply(HttpContext);

        var friends = await fRepo.FindAllAsync(new Friends { AccountId = user.AccountId });
        if (!friends.Any())
            return AccountErrors.AccountNotFound(accountId)
                .WithMessage($"Friends for user {accountId} not found.")
                .Apply(HttpContext);

        var friendAccounts = friends.Where(f => f.Status == "ACCEPTED").Select(f => f.FriendId).ToHashSet();
        var friendsInParty = partyMembers.Where(m => friendAccounts.Contains(m.AccountId)).Select(m => m.AccountId).ToList();

        var messageBody = new
        {
            expires = expiryTime,
            meta,
            ns = "Fortnite",
            party_id = party.PartyId,
            inviter_dn = inviter.Meta.GetValueOrDefault("urn:epic:member:dn_s", ""),
            invitee_id = user.AccountId,
            memebrs_count = partyMembers.Count,
            sent_at = timestamp,
            updated_by = timestamp,
            friends_ids = friendsInParty,
            sent = timestamp,
            type = "com.epicgames.social.party.notification.v0.INITIAL_INVITE"
        };

        var client = await Constants.repositoryPool.For<ClientSessions>().FindByColumnAsync("accountid", user.AccountId);
        if (client != null && Globals._socketConnections.TryGetValue(client.SocketId, out var socket))
        {
            var xmlns = XNamespace.Get("jabber:client");
            var xmlMessage = new XElement(xmlns + "message",
                new XAttribute("xmlns", "jabber:client"),
                new XAttribute("from", "xmpp-admin@prod.ol.epicgames.com"),
                new XAttribute("to", client.Jid),
                new XElement("body", JsonConvert.SerializeObject(messageBody))
            );

            socket.Send(xmlMessage.ToString(SaveOptions.DisableFormatting));
        }

        return Ok();
    }

    [HttpPost("Fortnite/user/{accountId}/pings/{pingerId}")]
    public async Task<IActionResult> CreatePing(string accountId, string pingerId)
    {
        var ping = await pingsRepo.FindAsync(new Pings { SentTo = accountId, SentBy = pingerId });
        if (ping != null)
            await pingsRepo.DeleteAsync(new Pings { SentTo = accountId, SentBy = pingerId });

        var now = DateTime.UtcNow;
        var expiryTime = now.AddHours(1);

        var newPing = new Pings
        {
            SentBy = pingerId,
            SentTo = accountId,
            SentAt = now.ToIsoUtcString(),
            ExpiresAt = expiryTime.ToIsoUtcString(),
            Meta = "{}"
        };
        await pingsRepo.SaveAsync(newPing);

        var user = await uRepo.FindByColumnAsync("accountid", pingerId);
        if (user == null)
            return AccountErrors.AccountNotFound(accountId).Apply(HttpContext);

        await Constants.GlobalXmppClientService.ForwardStanzaAsync(accountId, JsonConvert.SerializeObject(new
        {
            expires = newPing.ExpiresAt,
            meta = new Dictionary<string, string>(),
            ns = "Fortnite",
            pinger_dn = user.Username,
            pinger_id = pingerId,
            sent = newPing.SentAt,
            type = "com.epicgames.social.party.notification.v0.PING"
        }));

        return Ok(new
        {
            sent_by = newPing.SentBy,
            sent_to = newPing.SentTo,
            sent_at = newPing.SentAt,
            expires_at = newPing.ExpiresAt,
            meta = new Dictionary<string, string>()
        });
    }

    [HttpGet("Fortnite/user/{accountId}/pings/{pingerId}/parties")]
    public async Task<IActionResult> GetPartyFromPing(string accountId, string pingerId)
    {
        var parties = await pRepo.FindAllByTableAsync();
        var queriedPings = await pingsRepo.FindAllByColumnsAsync(new Dictionary<string, object> 
        {
            { "sentto", accountId },
            { "sentby", pingerId }
        });
        
        if (!queriedPings.Any())
        {
            queriedPings = new List<Pings> { new Pings { SentBy = pingerId } };
        }

        var partyWithMembers = parties.Select(party => new
        {
            Party = party,
            Members = string.IsNullOrEmpty(party.Members)
                ? new List<PartyMember>()
                : JsonSerializer.Deserialize<List<PartyMember>>(party.Members)
        }).ToList();

        var partyByMemberIdLookup = new Dictionary<string, (Parties Party, List<PartyMember> Members)>();
        foreach (var p in partyWithMembers)
        {
            foreach (var member in p.Members)
            {
                partyByMemberIdLookup[member.AccountId] = (p.Party, p.Members);
            }
        }

        var allMemberIds = new HashSet<string>();
        var result = new List<object>();

        foreach (var ping in queriedPings)
        {
            if (!partyByMemberIdLookup.TryGetValue(ping.SentBy, out var match))
                continue;

            var (party, members) = match;

            foreach (var member in members)
            {
                allMemberIds.Add(member.AccountId);
            }

            var partyConfig = string.IsNullOrEmpty(party.Config)
                ? new Dictionary<string, dynamic>()
                : JsonSerializer.Deserialize<Dictionary<string, dynamic>>(party.Config);

            var meta = string.IsNullOrEmpty(party.Meta)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(party.Meta);

            var memberList = members.Select(member => new
            {
                account_id = member.AccountId,
                meta = member.Meta,
                connections = string.IsNullOrEmpty(member.Connections)
                    ? new List<PartyMemberConnection>()
                    : JsonSerializer.Deserialize<List<PartyMemberConnection>>(member.Connections),
                revision = member.Revision,
                updated_at = member.UpdatedAt,
                joined_at = member.JoinedAt,
                role = member.Role
            }).ToList();

            var allInvites = await iRepo.FindAllByColumnAsync("sentby", allMemberIds.ToList(), useCache: false);
            
            result.Add(new
            {
                id = party.PartyId,
                created_at = party.CreatedAt,
                updated_at = party.UpdatedAt,
                config = partyConfig,
                members = memberList,
                applicants = new List<object>(),
                meta,
                invites = allInvites, 
                revision = party.Revision
            });
        }
        
        return Ok(result);
    }
}