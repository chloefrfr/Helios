using System.Text.Json.Serialization;
using Helios.Classes.Response;

namespace Helios.Utilities.Handlers.Wrappers;

[JsonSerializable(typeof(StandardApiError))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(string))]
public partial class SourceGenerationContext : JsonSerializerContext
{
    
}