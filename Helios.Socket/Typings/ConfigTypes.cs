using System.Xml.Serialization;

namespace Helios.Socket.Typings;

[XmlRoot("config", Namespace = WebSocketConfiguration.xmlString)]
public class ConfigTypes
{
    [XmlElement("DatabaseConnectionUrl")]
    public string DatabaseConnectionUrl { get; set; }
    [XmlElement("JWTClientSecret")]
    public string JWTClientSecret { get; set; }
    [XmlElement("GameDirectory")]
    public string GameDirectory { get; set; }
    [XmlElement("CurrentVersion")]
    public string CurrentVersion { get; set; }
}
