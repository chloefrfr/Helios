using Helios.Database.Tables.XMPP;

namespace Helios.Socket.Events;

public class ClientDisconnectedEventArgs : EventArgs
{
    public ClientSessions Client { get; set; }
    public string Reason { get; set; }
}