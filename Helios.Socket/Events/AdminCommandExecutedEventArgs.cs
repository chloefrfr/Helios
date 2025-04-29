using Helios.Database.Tables.XMPP;

namespace Helios.Socket.Events;

public class AdminCommandExecutedEventArgs : EventArgs
{
    public ClientSessions Client { get; set; }
    public string Command { get; set; }
    public bool Success { get; set; }
}