using Fleck;

namespace Helios.Socket;

public class Globals
{
    public static readonly Dictionary<Guid, IWebSocketConnection> _socketConnections = new();
}