using System.Collections.Concurrent;
using System.Xml.Linq;
using Fleck;
using Helios.Configuration;
using Helios.Database.Tables.XMPP;
using Helios.Socket;
using Helios.Socket.Classes;
using Helios.Socket.Events;
using Helios.Utilities;
using Helios.XMPP.Roots;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;
using WebSocketServer = Helios.Socket.WebSocketServer;

namespace Helios.XMPP;

public class XmppClient
{
    public WebSocketServer _server;      
    private static readonly ConcurrentDictionary<string, Delegate> Handlers = new ConcurrentDictionary<string, Delegate>
    {
        ["open"] = new Action<IWebSocketConnection, ClientSessions, XmppMessage>(OpenHandler.Handle),
        ["auth"] = new Func<IWebSocketConnection, ClientSessions, XmppMessage, Task>(AuthHandler.HandleAsync),
        ["iq"] = new Func<IWebSocketConnection, ClientSessions, XmppMessage, Task>(IqHandler.HandleAsync),
        ["presence"] = new Func<IWebSocketConnection, ClientSessions, XmppMessage, Task>(PresenceHandler.HandleAsync),
        ["message"] = new Func<IWebSocketConnection, ClientSessions, XmppMessage, Task>(MessageHandler.HandleAsync),
    };
    
    public async Task StartAsync()
    {
        _server = new WebSocketServer(new WebSocketConfiguration
        {
            ServerUrl = "ws://127.0.0.1:443",
            EnableLogging = false
        });
        

        _server.ClientConnected += Server_ClientConnected;
        _server.ClientDisconnected += Server_ClientDisconnected;
        _server.MessageReceived += Server_MessageReceived;
        _server.ErrorOccurred += Server_ErrorOccurred;
        
        _server.Start();
    }

    public async Task ForwardStanzaAsync(string accountId, string body)
    {
        var sessionRepo = Constants.repositoryPool.Repo<ClientSessions>();

        var clientSession = await sessionRepo().FindAsync(new ClientSessions { AccountId = accountId });

        if (clientSession is null)
            return;
        
        Logger.Debug($"SocketId: {clientSession.SocketId}");
        
        var stanza = new XElement(XNamespace.Get("jabber:client") + "message",
            new XAttribute("from", "xmpp-admin@prod.ol.epicgames.com"),
            new XAttribute("to", clientSession.Jid),
            new XAttribute("xmlns", "jabber:client"),
            new XElement("body", body)
        );

        await _server.SendToClient(clientSession.SocketId, stanza.ToString(SaveOptions.DisableFormatting)); 
    }

    public async Task ForwardPresenceStanzaAsync(string senderId, string receiverId, bool isOffline)
    {
        var sessionRepo = Constants.repositoryPool.Repo<ClientSessions>();

        var senderTask = sessionRepo().FindAsync(new ClientSessions { AccountId = senderId });
        var receiverTask = sessionRepo().FindAsync(new ClientSessions { AccountId = receiverId });

        await Task.WhenAll(senderTask, receiverTask);

        var sender = senderTask.Result;
        var receiver = receiverTask.Result;

        if (sender?.Jid is not string fromJid || receiver?.Jid is not string toJid)
            return;

        var lastPresence = JsonSerializer.Deserialize<LastPresenceUpdate>(sender.LastPresenceUpdate) ?? new LastPresenceUpdate();

        var presenceStanza = new XElement("presence",
            new XAttribute("from", fromJid),
            new XAttribute("to", toJid),
            new XAttribute("type", isOffline ? "unavailable" : "available")
        );

        if (!string.IsNullOrEmpty(lastPresence.StatusString))
        {
            if (lastPresence.IsAway)
                presenceStanza.Add(new XElement("show", "away"));

            presenceStanza.Add(new XElement("status", lastPresence.StatusString));
        }

        await _server.SendToClient(receiver.SocketId, presenceStanza.ToString(SaveOptions.DisableFormatting));
    }

    private static void Server_ClientConnected(object sender, ClientConnectedEventArgs e)
    {
        Logger.Info($"Client connected: {e.Client.SocketId}");
    }
    
    private static void Server_ClientDisconnected(object sender, ClientDisconnectedEventArgs e)
    {
        Logger.Info($"Client disconnected: {e.Client.SocketId}");
    }
    
    private async void Server_MessageReceived(object sender, MessageReceivedEventArgs e)
    {
        Logger.Info($"Message from {e.Client.SocketId}: {e.Message}");
        
        var socket = Globals._socketConnections[e.Client.SocketId];

        var clientSessionRepository = Constants.repositoryPool.Repo<ClientSessions>();
        var clientSession =
            await clientSessionRepository().FindAsync(new ClientSessions { SocketId = e.Client.SocketId });

        if (clientSession is null || socket is null)
            return;
        
        // TODO: Handle all of this in messageHandler
        if (!_server.TryParseXmppMessage(e.Message, out var xmppMessage))
            return;

        if (!Handlers.TryGetValue(xmppMessage.Type, out var handler))
        {
            Logger.Warn($"No handler found for root element: {xmppMessage.Type}");
            return;
        }

        Logger.Info($"Requested MessageType: {xmppMessage.Type}");

        try
        {
            switch (handler)
            {
                case Func<IWebSocketConnection, ClientSessions, XmppMessage, Task> asyncHandler:
                    await asyncHandler(socket, clientSession, xmppMessage);
                    break;
                case Action<IWebSocketConnection, ClientSessions, XmppMessage> syncHandler:
                    syncHandler(socket, clientSession, xmppMessage);
                    break;
                default:
                    Logger.Error($"Handler for '{xmppMessage.Type}' has an unsupported type.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error executing handler for root '{xmppMessage.Type}': {ex.Message}");
        }
        
        bool isValidConnection =
            !clientSession.IsLoggedIn &&
            clientSession.IsAuthenticated &&
            !string.IsNullOrEmpty(clientSession.AccountId) &&
            !string.IsNullOrEmpty(clientSession.DisplayName) &&
            !string.IsNullOrEmpty(clientSession.Jid) &&
            !string.IsNullOrEmpty(clientSession.Resource);

        if (isValidConnection)
        {
            clientSession.IsLoggedIn = true;
            await clientSessionRepository().UpdateAsync(clientSession);
    
            Logger.Info($"Successfully logged in as '{clientSession.DisplayName}'");
        }
    }
    
    private static void Server_ErrorOccurred(object sender, Helios.Socket.Events.ErrorEventArgs e)
    {
        Logger.Error($"Error: {e.ErrorSource} - {e.Error.Message}");
    }
}