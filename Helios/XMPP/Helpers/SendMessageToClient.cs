using System;
using System.Threading.Tasks;
using System.Xml.Linq;
using Fleck;
using Helios.Configuration;
using Helios.Database.Tables.XMPP;
using Helios.Socket;

namespace Helios.XMPP.Helpers;

public static class SendMessageToClient
{
    public static async Task<bool> SendAsync(IWebSocketConnection socket, string body, ClientSessions client, string to, string id)
    {
        try
        {
            var clientSessionsRepo = Constants.repositoryPool.Repo<ClientSessions>();
            var allClients = await clientSessionsRepo().FindAllByTableAsync();

            string bareJid = to.IndexOf('/') > -1 ? to[..to.IndexOf('/')] : to;
            
            var targetClient = allClients.FirstOrDefault(x => 
                (x.Jid.IndexOf('/') > -1 ? x.Jid[..x.Jid.IndexOf('/')] : x.Jid) == bareJid);

            if (targetClient == null)
                return false;

            var messageXml = new XElement("message",
                new XAttribute("from", client.Jid),
                new XAttribute("xmlns", "jabber:client"),
                new XAttribute("to", targetClient.Jid),
                new XAttribute("id", id),
                new XAttribute("type", "chat"),
                new XElement("body", body)
            );
            
            if (!Globals._socketConnections.TryGetValue(targetClient.SocketId, out var receiverSocket))
                return false;
            
            await receiverSocket.Send(messageXml.ToString(SaveOptions.DisableFormatting));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}