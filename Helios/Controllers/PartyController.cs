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
        var joinInfoMeta = joinInfo.SelectToken("meta")?.ToObject<Dictionary<string, object>>();
        var joinInfoConnectionMeta = joinInfo.SelectToken("connection.meta")?.ToObject<Dictionary<string, object>>();

        var existingParties = await pRepo.FindAllByTableAsync();
        var existingParty = existingParties.FirstOrDefault(p =>
        {
            var members = JsonSerializer.Deserialize<List<PartyMember>>(p.Members);
            return members.Any(m => m.AccountId == accountId);
        });

        var partyMemberConnection = new PartyMemberConnection
        {
            Id = connectionId ?? string.Empty,
            ConnectedAt = timestamp,
            UpdatedAt = timestamp,
            YieldLeadership = yieldLeadership,
            Meta = joinInfoConnectionMeta
        };

        if (existingParty != null)
        {
            var existingMembers = JsonSerializer.Deserialize<List<PartyMember>>(existingParty.Members);

            var existingMemberIndex = existingMembers.FindIndex(m => m.AccountId == accountId);

            if (existingMemberIndex != -1)
            {
                var existingMember = existingMembers[existingMemberIndex];
                existingMember.Meta = joinInfoMeta;
                existingMember.Connections = JsonSerializer.Serialize(new List<PartyMemberConnection> { partyMemberConnection });
                existingMember.UpdatedAt = timestamp;
            }
            else
            {
                var newPartyMember = new PartyMember
                {
                    AccountId = accountId,
                    Meta = joinInfoMeta,
                    Connections = JsonSerializer.Serialize(new List<PartyMemberConnection> { partyMemberConnection }),
                    Revision = existingMembers.Count,
                    UpdatedAt = timestamp,
                    JoinedAt = timestamp,
                    Role = existingMembers.Count == 0 ? "CAPTAIN" : "MEMBER"
                };
                existingMembers.Add(newPartyMember);
            }

            existingParty.UpdatedAt = timestamp;
            existingParty.Config = JsonSerializer.Serialize(requestData["config"].ToObject<Dictionary<string, dynamic>>());
            existingParty.Members = JsonSerializer.Serialize(existingMembers);
            existingParty.Meta = JsonSerializer.Serialize(requestData["meta"].ToObject<Dictionary<string, string>>());
            existingParty.Revision++;

            await pRepo.UpdateAsync(existingParty);

            return Ok(new
            {
                id = existingParty.PartyId,
                created_at = existingParty.CreatedAt,
                updated_at = timestamp,
                config = requestData["config"].ToObject<Dictionary<string, dynamic>>(),
                members = existingMembers.Select(m => new
                {
                    account_id = m.AccountId,
                    meta = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(m.Meta)),
                    connections = JsonSerializer.Deserialize<List<PartyMemberConnection>>(m.Connections),
                    revision = m.Revision,
                    updated_at = m.UpdatedAt,
                    joined_at = m.JoinedAt,
                    role = m.Role
                }).ToList(),
                meta = requestData["meta"].ToObject<Dictionary<string, string>>(),
                invites = JsonSerializer.Deserialize<List<PartyInvite>>(existingParty.Invites) ?? new List<PartyInvite>(),
                applicants = existingParty.Applicants,
                revision = existingParty.Revision,
                intentions = existingParty.Intentions
            });
        }
        else
        {
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
            max_number_of_members = partyConfig.TryGetValue("max_size", out var ms) ? ms.GetInt32() : 0,
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

        var memberMeta = requestData["meta"]?.ToObject<Dictionary<string, object>>() ?? new();
        var memberConnectionMeta = connectionInfo["meta"]?.ToObject<Dictionary<string, object>>() ?? new();

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
            return PartyErrors.MemberNotFound.WithMessage($"Member {accountId} not found in party {partyId}.").Apply(HttpContext);

        var removedMember = partyMembers[memberIndex];
        partyMembers.RemoveAt(memberIndex);

        party.Revision++;
        party.UpdatedAt = timestamp;
        party.Members = JsonSerializer.Serialize(partyMembers);

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

        if (partyMembers.Count == 0)
        {
            await pRepo.DeleteByColumnAsync("partyid", party.PartyId);
            return Ok();
        }

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
                }

                party.UpdatedAt = timestamp;
                party.Meta = JsonSerializer.Serialize(partyMeta);
                party.Members = JsonSerializer.Serialize(partyMembers);

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
                    { assignmentKey, JsonConvert.SerializeObject(rawSquadAssignment) }
                },
                    party_sub_type = partySubType,
                    party_type = "DEFAULT",
                    revision = party.Revision,
                    sent = timestamp,
                    type = "com.epicgames.social.party.notification.v0.PARTY_UPDATED",
                    updated_at = party.UpdatedAt
                });

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