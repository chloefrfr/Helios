namespace Helios.Socket.Classes;

public class MessageData
{
    public string RawContent { get; set; }
    public bool IsXmpp { get; set; }
    public XmppMessage XmppMessage { get; set; }
}