using System.Text.Json;
using System.Xml.Linq;
using Helios.Configuration;
using Helios.Database.Tables.XMPP;
using Helios.Socket;

namespace Helios.XMPP.Helpers;

public static class GetUserPresence
{
    public static async Task GetAsync(bool offline, string senderId, string receiverId)
    {
        var clientSessionsRepo = Constants.repositoryPool.Repo<ClientSessions>();

        var sender = await clientSessionsRepo().FindAsync(new ClientSessions { AccountId = senderId });
        var receiver = await clientSessionsRepo().FindAsync(new ClientSessions { AccountId = receiverId });

        if (sender == null || receiver == null || !Globals._socketConnections.TryGetValue(receiver.SocketId, out var receiverSocket)) return;

        var lastPresence = JsonSerializer.Deserialize<LastPresenceUpdate>(sender.LastPresenceUpdate) ?? new LastPresenceUpdate();
        var presenceXml = BuildPresenceXml(sender.Jid, receiver.Jid, lastPresence, offline);

        receiverSocket.Send(presenceXml);
    }

    private static string BuildPresenceXml(string fromJid, string toJid, LastPresenceUpdate presence, bool offline)
    {
        var presenceType = offline ? "unavailable" : "available";

        var xml = new XElement(XNamespace.Get("jabber:client") + "presence",
            new XAttribute("from", fromJid),
            new XAttribute("to", toJid),
            new XAttribute("type", presenceType)
        );

        if (presence.IsAway)
        {
            xml.Add(new XElement("show", "away"));
        }

        xml.Add(new XElement("status", presence.StatusString));

        return xml.ToString(SaveOptions.DisableFormatting).Replace(" xmlns=\"\"", "");
    }
}