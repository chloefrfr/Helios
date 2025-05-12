using System.Text.Json;
using System.Xml.Linq;
using Helios.Classes.Party.SquadAssignments;
using Helios.Configuration;
using Helios.Database.Repository;
using Helios.Database.Tables.Account;
using Helios.Database.Tables.Fortnite;
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

        var configUpdates = requestData["config"]?.ToObject<Dictionary<string, object>>();
        var meta = requestData["meta"];
        var metaDelete = meta?["delete"]?.Values<string>().ToList();
        var metaUpdate = meta?["update"]?.ToObject<Dictionary<string, string>>();

        if (configUpdates != null && configUpdates.Count > 0)
        {
            var existingConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(party.Config) ?? new();

            foreach (var (key, value) in configUpdates)
                existingConfig[key] = value;

            party.Config = JsonSerializer.Serialize(existingConfig);
        }

        var partyMeta = JsonSerializer.Deserialize<Dictionary<string, object>>(party.Meta) ?? new();
        bool metaModified = false;

        if (metaDelete != null && metaDelete.Count > 0)
        {
            foreach (var key in metaDelete)
                metaModified |= partyMeta.Remove(key);
        }

        if (metaUpdate != null && metaUpdate.Count > 0)
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

        var partyConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(party.Config) ?? new();

        object GetSafeValue(string key)
        {
            if (partyConfig.TryGetValue(key, out var element))
            {
                return element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number => element.GetInt32(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    JsonValueKind.Object => JsonSerializer.Deserialize<object>(element.GetRawText()),
                    JsonValueKind.Array => JsonSerializer.Deserialize<object[]>(element.GetRawText()),
                    _ => null
                };
            }
            return null;
        }

        var messageTemplate = new
        {
            captain_id = captain?.AccountId,
            party_state_updated = metaUpdate,
            party_state_removed = metaDelete,
            party_state_overriden = metaUpdate,
            party_privacy_type = GetSafeValue("joinability"),
            party_type = GetSafeValue("type") ?? "DEFAULT",
            party_sub_type = partyMeta["urn:epic:cfg:party-type-id_s"]?.ToString(),
            max_number_of_members = GetSafeValue("max_size"),
            invite_ttl_seconds = GetSafeValue("invite_ttl") ?? 14400,
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

        var metaDelete = requestData["delete"]?.ToObject<List<string>>() ?? new List<string>();
        var metaUpdate = requestData["update"]?.ToObject<Dictionary<string, object>>();

        var partyMembers = JsonSerializer.Deserialize<List<PartyMember>>(party.Members);
        var member = partyMembers.FirstOrDefault(m => m.AccountId == accountId);
        if (member == null)
            return PartyErrors.MemberNotFound.WithMessage($"Member {accountId} not found in party {partyId}.").Apply(HttpContext);

        var currentTime = DateTime.UtcNow;
        string isoTime = currentTime.ToIsoUtcString();

        member.UpdatedAt = isoTime;

        Dictionary<string, object> partyMeta = null;
        if ((metaDelete?.Count > 0) || (metaUpdate?.Count > 0))
        {
            partyMeta = JsonSerializer.Deserialize<Dictionary<string, object>>(party.Meta) ?? new();

            foreach (var key in metaDelete)
            {
                partyMeta.Remove(key);
            }

            if (metaUpdate != null)
            {
                foreach (var (key, value) in metaUpdate)
                {
                    partyMeta[key] = value;
                }

                party.Meta = JsonSerializer.Serialize(partyMeta);
            }
        }

        party.Members = JsonSerializer.Serialize(partyMembers);
        party.UpdatedAt = isoTime;
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
        var connectionInfo = requestData["connection"] ?? throw new InvalidOperationException("Missing connection.");
        var memberId = connectionInfo.SelectToken("id")?.ToString().Split("@prod")[0] ?? string.Empty;
        var connectionId = connectionInfo.SelectToken("id")?.ToString();
        var memberMeta = requestData.SelectToken("meta")?.ToObject<Dictionary<string, object>>();
        var memberConnectionMeta = connectionInfo.SelectToken("meta")?.ToObject<Dictionary<string, object>>();
        var yieldLeadership = connectionInfo.SelectToken("yield_leadership")?.Value<bool>() ?? false;


        var partyMemberConnection = new PartyMemberConnection
        {
            Id = memberId,
            ConnectedAt = timestamp,
            UpdatedAt = timestamp,
            YieldLeadership = yieldLeadership,
            Meta = memberConnectionMeta
        };

        var deserializedPartyMembers = JsonSerializer.Deserialize<List<PartyMember>>(party.Members);
        // deserializedPartyMembers = deserializedPartyMembers
        //     .Where(member => member.AccountId != accountId)
        //     .ToList();

        deserializedPartyMembers.Add(new PartyMember
        {
            AccountId = memberId,
            Meta = memberMeta,
            Connections = JsonSerializer.Serialize(new List<PartyMemberConnection> { partyMemberConnection }),
            Revision = 0,
            UpdatedAt = timestamp,
            JoinedAt = timestamp,
            Role = yieldLeadership ? "CAPTAIN" : "MEMBER"
        });

        var partyMeta = JsonSerializer.Deserialize<Dictionary<string, object>>(party.Meta) ?? new();
        string squadAssignmentsKey = partyMeta.ContainsKey("Default:RawSquadAssignments_j")
            ? "Default:RawSquadAssignments_j"
            : "RawSquadAssignments_j";

        string rawJson = partyMeta.ContainsKey(squadAssignmentsKey)
            ? partyMeta[squadAssignmentsKey]?.ToString()
            : "{\"RawSquadAssignments\":[]}";

        var squadAssignments = JsonConvert.DeserializeObject<SquadAssignmentsWrapper>(rawJson);
        squadAssignments.RawSquadAssignments.Add(new SquadAssignment
        {
            MemberId = memberId,
            AbsoluteMemberIdx = deserializedPartyMembers.Count - 1
        });

        partyMeta[squadAssignmentsKey] = JsonConvert.SerializeObject(squadAssignments);
        party.Revision++;
        party.UpdatedAt = timestamp;
        party.Members = JsonSerializer.Serialize(deserializedPartyMembers);
        party.Meta = JsonSerializer.Serialize(partyMeta);

        await pRepo.UpdateAsync(party);

        var partyCaptain = deserializedPartyMembers.FirstOrDefault(x => x.Role == "CAPTAIN");
        var partyConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(party.Config) ?? new();

        var memberJoinedMessage = new
        {
            account_id = memberId,
            account_dn = memberMeta["urn:epic:member:dn_s"],
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

        string privacyType = "PUBLIC";
        if (partyConfig.ContainsKey("joinability"))
            privacyType = partyConfig["joinability"].ToString();

        string partySubType = null;
        if (partyMeta.ContainsKey("urn:epic:cfg:party-type-id_s"))
            partySubType = partyMeta["urn:epic:cfg:party-type-id_s"].ToString();

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
                {
                    squadAssignmentsKey, JsonConvert.SerializeObject(squadAssignments)
                }
            },
            party_sub_type = partySubType,
            party_type = "DEFAULT",
            revision = party.Revision,
            sent = timestamp,
            type = "com.epicgames.social.party.notification.v0.PARTY_UPDATED",
            updated_at = timestamp,
        };

        string jsonMemberJoined = JsonConvert.SerializeObject(memberJoinedMessage);
        string jsonPartyUpdated = JsonConvert.SerializeObject(partyUpdatedMessage);

        var sessionsRepo = Constants.repositoryPool.For<ClientSessions>();
        var accountIds = deserializedPartyMembers.Select(m => m.AccountId).ToList();
        var clients = await sessionsRepo.FindAllByColumnAsync("accountid", accountIds);
        var clientMap = clients.ToDictionary(c => c.AccountId);

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
            return PartyErrors.MemberNotFound.WithMessage($"Member {accountId} not found in party {partyId}.").Apply(HttpContext);

        var removedMember = partyMembers[memberIndex];
        partyMembers.RemoveAt(memberIndex);

        party.Revision++;
        party.UpdatedAt = timestamp;
        party.Members = JsonSerializer.Serialize(partyMembers);

        var accountIds = partyMembers.Select(m => m.AccountId).ToList();
        var clients = await Constants.repositoryPool.For<ClientSessions>()
            .FindAllByColumnAsync("accountid", accountIds);

        var socketMap = clients
            .Where(c => Globals._socketConnections.ContainsKey(c.SocketId))
            .ToDictionary(
                c => c.AccountId,
                c => new { Client = c, Socket = Globals._socketConnections[c.SocketId] }
            );

        var memberLeftBody = JsonConvert.SerializeObject(new
        {
            account_id = removedMember.AccountId,
            party_id = party.PartyId,
            kicked = true,
            updated_at = timestamp,
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

        foreach (var member in partyMembers)
        {
            if (!socketMap.TryGetValue(member.AccountId, out var clientInfo))
                continue;

            var xml = new XElement(baseXml);
            xml.SetAttributeValue("to", clientInfo.Client.Jid);
            clientInfo.Socket.Send(xml.ToString(SaveOptions.DisableFormatting));
        }

        if (partyMembers.Count == 0)
        {
            await pRepo.DeleteByColumnAsync("partyid", party.PartyId);
            return Ok();
        }

        await pRepo.UpdateAsync(party);

        var partyMeta = JsonSerializer.Deserialize<Dictionary<string, object>>(party.Meta) ?? new();

        string assignmentKey;
        if (partyMeta.ContainsKey("Default:RawSquadAssignments_j"))
            assignmentKey = partyMeta["Default:RawSquadAssignments_j"].ToString();
        else
            assignmentKey = "RawSquadAssignments_j";

        if (partyMeta.ContainsKey(assignmentKey))
        {
            var jsonString = partyMeta[assignmentKey]?.ToString();
            if (!string.IsNullOrEmpty(jsonString))
            {
                var rawSquadAssignment = JsonConvert.DeserializeObject<SquadAssignmentsWrapper>(jsonString);
                var index = rawSquadAssignment.RawSquadAssignments.FindIndex(x => x.MemberId == accountId);

                if (index != -1)
                {
                    rawSquadAssignment.RawSquadAssignments.RemoveAt(index);
                    partyMeta[assignmentKey] = JsonSerializer.Serialize(rawSquadAssignment);
                }

                var captain = partyMembers.FirstOrDefault(x => x.Role == "CAPTAIN");
                if (captain == null && partyMembers.Count > 0)
                {
                    partyMembers[0].Role = "CAPTAIN";
                    captain = partyMembers[0];
                }

                party.UpdatedAt = timestamp;
                party.Meta = JsonSerializer.Serialize(partyMeta);

                await pRepo.UpdateAsync(party);

                var partyConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(party.Config) ?? new();
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
                    max_number_of_members = 16,
                    ns = "Fortnite",
                    party_id = party.PartyId,
                    party_privacy_type = privacyType,
                    party_state_overriden = new Dictionary<string, object>(),
                    party_state_removed = new List<string>(),
                    party_state_updated = new Dictionary<string, object>
                    {
                        { assignmentKey, JsonConvert.SerializeObject(partyMeta[assignmentKey]) }
                    },
                    party_sub_type = partySubType,
                    party_type = "DEFAULT",
                    revision = party.Revision,
                    sent = timestamp,
                    type = "com.epicgames.social.party.notification.v0.PARTY_UPDATED",
                    updated_at = party.UpdatedAt
                });

                Console.WriteLine(JsonConvert.SerializeObject(updateBody, Formatting.Indented));

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

        return Ok();
    }
}