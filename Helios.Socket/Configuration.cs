using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Helios.Socket.Typings;

namespace Helios.Socket;

public static class Configuration
{
    public static ConfigTypes Load()
    {
        var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "config.xml");
        
        if (!File.Exists(configFile))
            throw new FileNotFoundException($"Config file '{configFile}' not found.");

        try
        {
            var xmlDoc = new System.Xml.XmlDocument();
            xmlDoc.Load(configFile);
            ValidateXml(xmlDoc);

            using (var reader = new StringReader(File.ReadAllText(configFile)))
            {
                var serializer = new XmlSerializer(typeof(ConfigTypes));

                var settings = new XmlReaderSettings();
                var xmlNamespace = new XmlSerializerNamespaces();
                xmlNamespace.Add(string.Empty, string.Empty); 

                using (var xmlReader = XmlReader.Create(reader, settings))
                {
                    return (ConfigTypes)serializer.Deserialize(xmlReader);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load config: {ex.Message}");
            throw new ApplicationException("Error loading config file", ex);
        }
    }
    
    private static void ValidateXml(System.Xml.XmlDocument xmlDoc)
    {
        var schemaFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Schemas", "ConfigSchema.xsd");

        if (!File.Exists(schemaFile))
        {
            Logger.Warn("XML Schema file 'ConfigSchema.xsd' not found. XML validation will be skipped.");
            return;
        }

        var xmlSchemaSet = new XmlSchemaSet();
        xmlSchemaSet.Add(WebSocketConfiguration.xmlString, schemaFile);

        xmlDoc.Schemas.Add(xmlSchemaSet);
        xmlDoc.Validate((sender, args) =>
        {
            if (args.Severity == XmlSeverityType.Error)
            {
                Logger.Error($"XML Validation Error: {args.Message}");
                throw new InvalidOperationException("Invalid XML format.");
            }
        });
    }
    
    public static ConfigTypes GetConfig()
    {
        try
        {
            return Load();
        }
        catch (FileNotFoundException ex)
        {
            Logger.Error($"Config file not found: {ex.Message}");
            throw;
        }
        catch (InvalidOperationException ex)
        {
            Logger.Error($"Invalid XML format: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error($"Unexpected error occurred: {ex.Message}");
            throw new ApplicationException("Unexpected error while loading the config", ex);
        }
    }
}