using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Fleck;
using Helios.Database.Tables.XMPP;
using Helios.Socket.Classes;
using Helios.Utilities;
using Helios.XMPP.Helpers;

namespace Helios.XMPP.Roots;

public static class MessageHandler
{
    public static async Task HandleAsync(IWebSocketConnection socket, ClientSessions client, XmppMessage root)
    {
        try
        {
            if (root?.Element == null)
            {
                Logger.Error("Received null message or element");
                return;
            }

            var bodyElement = root.Element.Elements().FirstOrDefault(x => x.Name.LocalName == "body");
            if (bodyElement == null)
            {
                Logger.Debug("Message received with no body element");
                return;
            }

            string body = bodyElement.Value;
            if (string.IsNullOrEmpty(body))
            {
                Logger.Debug("Empty message body received");
            }
            
            Logger.Debug($"Message body: {body}");
            
            var toAttribute = root.Element.Attribute("to");
            if (toAttribute == null)
            {
                Logger.Error("Message missing 'to' attribute");
                return;
            }
            string to = toAttribute.Value;
            
            var idAttribute = root.Element.Attribute("id");
            string id = idAttribute?.Value ?? Guid.NewGuid().ToString();
            
            bool delivered = await SendMessageToClient.SendAsync(socket, body, client, to, id);
            
            if (!delivered)
                return;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error handling message: {ex.Message}");
        }
    }
}