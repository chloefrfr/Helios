using System.Text.Json;

namespace Helios.Socket.Classes;

public class AdminMessage
{
    public string Type { get; set; }
    public string Token { get; set; }
    public JsonElement? Payload { get; set; }
}