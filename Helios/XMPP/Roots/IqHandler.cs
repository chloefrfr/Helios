using System.Text.Json;
using System.Xml.Linq;
using Fleck;
using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.Database.Tables.XMPP;
using Helios.Socket.Classes;
using Helios.Utilities;

namespace Helios.XMPP.Roots;

public class IqHandler
{
    public static async Task HandleAsync(IWebSocketConnection socket, ClientSessions client, XmppMessage root)
    {
        if (client is null)
            return;
        
        var attributeId = root.Element.Attribute("id")?.Value;
        var clientSessionsRepository = Constants.repositoryPool.GetRepository<ClientSessions>();
        var friendsRepository = Constants.repositoryPool.GetRepository<Friends>();
        
        // var existingClientSession =
        //     await clientSessionsRepository.FindAsync(new ClientSessions { AccountId = client.AccountId });
        //
        switch (attributeId)
        {
            case "_xmpp_bind1":
                var resource = root.Element.Elements().Where(x => x.Name.LocalName == "bind").First().Elements().First().Value.ToString();
                if (client.Resource is null && client.AccountId != null)
                {
                    client.Resource = resource;
                    client.Jid = $"{client.AccountId}@prod.ol.epicgames.com/{client.Resource}";
                    
                    await clientSessionsRepository.UpdateAsync(client);
                    
                    socket.Send(
                        new XElement(XNamespace.Get("jabber:client") + "iq",
                            new XAttribute("to", client.Jid),
                            new XAttribute("id", "_xmpp_bind1"),
                            new XAttribute("type", "result"),
                            new XElement(XNamespace.Get("urn:ietf:params:xml:ns:xmpp-bind") + "bind",
                                new XElement(XNamespace.Get("urn:ietf:params:xml:ns:xmpp-bind") + "jid", client.Jid)
                            )
                        ).ToString()
                    );
                }
                else
                {
                    socket.Send("<close xmlns='urn:ietf:params:xml:ns:xmpp-framing'/>");
                    socket.Close();
                }
                break;
            case "_xmpp_session1":
                socket.Send(new XElement(XNamespace.Get("jabber:client") + "iq",
                    new XAttribute("to", client.Jid),
                    new XAttribute("from", "prod.ol.epicgames.com"),
                    new XAttribute("id", "_xmpp_session1"),
                    new XAttribute("xmlns", "jabber:client"),
                    new XAttribute("type", "result")
                ).ToString());
                
                var user = await friendsRepository.FindAllAsync(new Friends { AccountId = client.AccountId });
                if (user is null)
                {
                    Logger.Error($"User '{client.AccountId}' not found.");
                    socket.Close();
                    return;
                }
                
                foreach (var friend in user)
                {
                    if (friend.Status != "ACCEPTED")
                        continue;

                    var cl = await clientSessionsRepository.FindAsync(new ClientSessions { AccountId = friend.AccountId });
                    if (cl is null)
                    {
                        Logger.Error($"Friend with AccountId '{friend.AccountId}' not found.");
                        socket.Close();
                        return;
                    }

                    var deserializedLastPresenceUpdate = cl.LastPresenceUpdate != null 
                        ? JsonSerializer.Deserialize<LastPresenceUpdate>(cl.LastPresenceUpdate)
                        : new LastPresenceUpdate();

                    try {
                        var presenceXml = new XElement("presence",
                            new XAttribute("to", client.Jid),
                            new XAttribute("xmlns", "jabber:client"),
                            new XAttribute("from", cl.Jid),
                            new XAttribute("type", "available"),
                            deserializedLastPresenceUpdate.IsAway ? new XElement("show", "away") : null,
                            new XElement("status", deserializedLastPresenceUpdate.StatusString ?? string.Empty)
                        );
    
                        socket.Send(presenceXml.ToString());
                    } catch (Exception ex) {
                        Logger.Error($"Error creating presence XML for {cl.AccountId}: {ex.Message}");
                    }
                }
                break;
            default:          
                Logger.Warn($"Missing attributeId: {attributeId}");
                socket.Send(
                    new XElement(XNamespace.Get("jabber:client") + "iq",
                        new XAttribute("to", client.Jid),
                        new XAttribute("from", "prod.ol.epicgames.com"),
                        new XAttribute("id", attributeId),
                        new XAttribute("type", "result")
                    ).ToString()
                );
                break;
        }
    }
}