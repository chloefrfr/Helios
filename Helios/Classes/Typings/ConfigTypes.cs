using System.Xml.Serialization;
using Helios.Configuration;

namespace Helios.Classes.Typings;

[XmlRoot("config", Namespace = Constants.xmlString)]
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
