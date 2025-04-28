using Helios.Socket.Classes;

namespace Helios.Socket.Events;

public class XmppMessageEventArgs : EventArgs
{
    public Guid SocketId { get; set; }
    public XmppMessage Message { get; set; }
}