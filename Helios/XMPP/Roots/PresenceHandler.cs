using System.Xml.Linq;
using Fleck;
using Helios.Database.Tables.XMPP;
using Helios.Socket.Classes;
using Helios.Utilities;
using Helios.XMPP.Helpers;
using Newtonsoft.Json;

namespace Helios.XMPP.Roots;

public static class PresenceHandler
{
    public static async Task HandleAsync(IWebSocketConnection socket, ClientSessions client, XmppMessage root)
    {
        var element = root.Element;
        if (element == null) return;

        string? type = element.Attribute("type")?.Value;
        string? to = element.Attribute("to")?.Value;

        if (!string.IsNullOrEmpty(type))
        {
            Logger.Info($"Root Type: {type}");

            if (type == "unavailable")
            {
                return;
            }
        }

        bool hasXElement = element.Elements().Any(e => e.Name.LocalName == "x");
        if (hasXElement)
        {
        }

        var statusElement = element.Elements().FirstOrDefault(e => e.Name.LocalName == "status");
        if (statusElement == null) return;

        try
        {
            bool isAway = element.Elements().Any(e => e.Name.LocalName == "show");

            await UpdatePresenceForFriend.UpdateAsync(socket, statusElement, false, isAway);
            await GetUserPresence.GetAsync(false, client.AccountId, client.AccountId);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to parse status JSON: {ex.Message}");
        }
    }
}