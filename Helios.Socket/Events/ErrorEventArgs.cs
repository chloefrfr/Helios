using Helios.Database.Tables.XMPP;

namespace Helios.Socket.Events;

public class ErrorEventArgs : EventArgs
{
    public ClientSessions Client { get; set; }
    public Exception Error { get; set; }
    public string ErrorSource { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}