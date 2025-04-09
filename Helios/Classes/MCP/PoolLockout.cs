using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Helios.Classes.MCP;

public class PoolLockout
{
    [JsonPropertyName("lockoutName")]
    public string LockoutName { get; set; }
}