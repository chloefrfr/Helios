using Helios.Database.Tables.XMPP;

namespace Helios.Socket.Events;

public class MessageReceivedEventArgs : EventArgs
{
    public ClientSessions Client { get; set; }
    public string Message { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}