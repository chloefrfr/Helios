using System.Xml.Linq;

namespace Helios.Socket.Classes;

public class XmppMessage
{
    public string Type { get; set; }
    public string Namespace { get; set; }
    public string From { get; set; }
    public string To { get; set; }
    public string Id { get; set; }
    public string Version { get; set; }
    public XElement Element { get; set; }
    public string RawContent { get; set; }
}