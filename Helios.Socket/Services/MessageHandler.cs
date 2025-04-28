using System.Text.Json;
using System.Xml.Linq;
using Fleck;
using Helios.Socket.Classes;
using Helios.Socket.Events;
using Helios.Socket.Interfaces;

namespace Helios.Socket.Services;

public class MessageHandler
{
    private readonly IClientManager _clientManager;
    
    public event EventHandler<XmppMessageEventArgs> XmppMessageReceived;
    
    public MessageHandler(IClientManager clientManager)
    {
        _clientManager = clientManager ?? throw new ArgumentNullException(nameof(clientManager));
    }
    
    public async Task SendToClient(Guid socketId, string message)
    {
        var (success, client) = await _clientManager.TryGetClientAsync(socketId);
        if (success && client != null)
        {
            try
            {
                Globals._socketConnections[client.SocketId].Send(message);
                Logger.Debug($"Message sent to client {socketId}: {message}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error sending message to client {socketId}: {ex.Message}");
                throw;
            }
        }   
        else
        {
            Logger.Warn($"Client {socketId} is not connected or not available");
            throw new InvalidOperationException($"Client {socketId} is not connected or not available");
        }
    }
    
    
    public async Task BroadcastMessage(string message)
    {
        var clients = await _clientManager.ListAllClients();
        var tasks = new List<Task>();

        foreach (var client in clients)
        {
            var socket = Globals._socketConnections[client.SocketId];
            if (socket.IsAvailable)
                tasks.Add(socket.Send(message));
        }

        try
        {
            await Task.WhenAll(tasks);
            Logger.Debug($"Broadcast message sent to {tasks.Count} clients: {message}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error during broadcast: {ex.Message}");
            throw;
        }
    }

    public async Task BroadcastMessageExcept(Guid excludedSocketId, string message)
    {
        var clients = await _clientManager.ListAllClients();
        var tasks = new List<Task>();

        foreach (var client in clients)
        {
            var socket = Globals._socketConnections[client.SocketId];
            if (socket.IsAvailable && client.SocketId != excludedSocketId)
                tasks.Add(socket.Send(message));
        }

        try
        {
            await Task.WhenAll(tasks);
            Logger.Debug($"Broadcast message sent to {tasks.Count} clients (excluding {excludedSocketId}): {message}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error during broadcast: {ex.Message}");
            throw;
        }
    }
    
    public bool TryParseXmppMessage(string message, out XmppMessage xmppMessage)
    {
        xmppMessage = null;
            
        try
        {
            if (!(message.StartsWith("<") && message.EndsWith(">")))
                return false;

            var element = XElement.Parse(message);
                
            xmppMessage = new XmppMessage
            {
                Type = element.Name.LocalName,
                Namespace = element.Name.NamespaceName,
                To = element.Attribute("to")?.Value,
                From = element.Attribute("from")?.Value,
                Id = element.Attribute("id")?.Value,
                Version = element.Attribute("version")?.Value,
                Element = element,
                RawContent = message
            };
                
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to parse XML message: {ex.Message}");
            return false;
        }
    }
}