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
}
