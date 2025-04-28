using Helios.Socket.Classes;

namespace Helios.Socket.Interfaces;

public interface IWebSocketServer
{
    bool IsRunning { get; }
    void Start();
    Task Stop();
    Task SendToClient(Guid socketId, string message);
    Task BroadcastMessage(string message);
    Task BroadcastMessageExcept(Guid excludedSocketId, string message);
    bool TryParseXmppMessage(string message, out XmppMessage xmppMessage);

    event EventHandler<Events.ClientConnectedEventArgs> ClientConnected;
    event EventHandler<Events.ClientDisconnectedEventArgs> ClientDisconnected;
    event EventHandler<Events.MessageReceivedEventArgs> MessageReceived;
    event EventHandler<Events.ErrorEventArgs> ErrorOccurred;
}