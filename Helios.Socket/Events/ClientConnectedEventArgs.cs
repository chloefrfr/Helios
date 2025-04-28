using Helios.Database.Tables.XMPP;

namespace Helios.Socket.Events;

public class ClientConnectedEventArgs : EventArgs
{
    public ClientSessions Client { get; set; }
}