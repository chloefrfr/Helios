using System.Xml.Linq;
using Fleck;
using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.Database.Tables.XMPP;
using Helios.Socket;
using Helios.Utilities;
using Newtonsoft.Json;

namespace Helios.XMPP.Helpers;

public static class UpdatePresenceForFriend
{
    public static async Task UpdateAsync(IWebSocketConnection socket, XElement status, bool offline, bool away)
    {
        var clientSessionsRepo = Constants.repositoryPool.Repo<ClientSessions>();
        var friendsRepo = Constants.repositoryPool.Repo<Friends>();

        var sender = await clientSessionsRepo().FindAsync(new ClientSessions { SocketId = socket.ConnectionInfo.Id });
        if (sender == null) return;

        var lastPresence = JsonConvert.DeserializeObject<LastPresenceUpdate>(sender.LastPresenceUpdate) ?? new LastPresenceUpdate();
        lastPresence.IsAway = away;
        lastPresence.StatusString = status == null ? lastPresence.StatusString : status.Value;
        
        sender.LastPresenceUpdate = JsonConvert.SerializeObject(lastPresence);
        await clientSessionsRepo().UpdateAsync(sender);

        var friends = await friendsRepo().FindAllAsync(new Friends { AccountId = sender.AccountId });
        if (friends == null) return;

        foreach (var friend in friends.Where(f => f.Status == "ACCEPTED"))
        {
            var client = await clientSessionsRepo().FindAsync(new ClientSessions { AccountId = friend.AccountId });
            if (client == null || !Globals._socketConnections.TryGetValue(client.SocketId, out var friendSocket)) continue;

            var presence = BuildPresenceXml(sender.Jid, client.Jid, lastPresence, offline);
            friendSocket.Send(presence);
        }
    }

    private static string BuildPresenceXml(string fromJid, string toJid, LastPresenceUpdate presence, bool offline)
    {
        var presenceType = offline ? "unavailable" : "available";

        var xml = new XElement(XNamespace.Get("jabber:client") + "presence",
            new XAttribute("from", fromJid),
            new XAttribute("to", toJid),
            new XAttribute("type", presenceType),
            new XAttribute("xmlns", XNamespace.Get("jabber:client"))
        );

        if (presence.IsAway)
        {
            xml.Add(new XElement("show", "away"));
        }

        xml.Add(new XElement("status", presence.StatusString));

        return xml.ToString(SaveOptions.DisableFormatting).Replace(" xmlns=\"\"", "");
    }
}
