using System.Xml.Linq;
using Fleck;
using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.Database.Tables.XMPP;
using Helios.Socket;
using Helios.Socket.Classes;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Helios.XMPP.Roots;

public static class PresenceHandler
{
    public static async Task HandleAsync(IWebSocketConnection socket, ClientSessions client, XmppMessage root)
    {
        var element = root.Element;
        if (element == null) return;

        string? type = element.Attribute("type")?.Value;
        if (type == "unavailable") return;

        var statusElement = element.Elements().FirstOrDefault(e => e.Name.LocalName == "status");
        if (statusElement == null) return;

        var showElement = element.Elements().FirstOrDefault(x => x.Name.LocalName == "show");
        bool isAway = showElement != null;
        string statusValue = statusElement.Value;
        
        var lastPresence = new LastPresenceUpdate
        {
            StatusString = statusValue,
            IsAway = isAway
        };
        client.LastPresenceUpdate = JsonSerializer.Serialize(lastPresence);

        var clientSessionsRepo = Constants.repositoryPool.Repo<ClientSessions>();
        await clientSessionsRepo().UpdateAsync(client);

        var clientNamespace = XNamespace.Get("jabber:client");
        var presenceAttrs = new[]
        {
            new XAttribute("to", client.Jid),
            new XAttribute("from", client.Jid),
            new XAttribute("xmlns", "jabber:client"),
            new XAttribute("type", "available")
        };

        var presenceResponse = new XElement(clientNamespace + "presence", presenceAttrs);
        presenceResponse.Add(new XElement(clientNamespace + "status", statusValue));
        if (isAway) presenceResponse.Add(new XElement(clientNamespace + "show", "away"));

        await socket.Send(presenceResponse.ToString());

        var fRepo = Constants.repositoryPool.Repo<Friends>();
        var friends = await fRepo().FindAllAsync(new Friends { AccountId = client.AccountId });
        if (friends == null) return;

        foreach (var friend in friends)
        {
            if (friend.Status != "ACCEPTED") continue;

            var friendClient = await clientSessionsRepo().FindByColumnAsync("accountid", friend.FriendId);
            if (!Globals._socketConnections.TryGetValue(friendClient.SocketId, out var friendClientSocket) || friendClient == null) continue;

            var friendMessage = new XElement(clientNamespace + "presence",
                new XAttribute("to", friendClient.Jid),
                new XAttribute("from", client.Jid),
                new XAttribute("xmlns", "jabber:client"),
                new XAttribute("type", "available"),
                new XElement(clientNamespace + "status", statusValue));

            if (isAway) friendMessage.Add(new XElement(clientNamespace + "show", "away"));
            await friendClientSocket.Send(friendMessage.ToString());
        }
    }
}