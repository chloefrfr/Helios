using Fleck;
using Helios.Database.Repository;
using Helios.Database.Tables.XMPP;

namespace Helios.Socket.Interfaces;

public interface IClientManager
{
    Repository<ClientSessions> Clients { get; set; }
    
    Task AddClient(ClientSessions client, IWebSocketConnection socket);
    Task RemoveClient(string accountId);
    Task RemoveClient(IWebSocketConnection connection);
    Task<bool> ClientExists(Guid guid);
    Task RemoveClient(Guid guid);
    Task<(bool success, ClientSessions? client)> TryGetClientAsync(string accountId);
    Task<(bool success, ClientSessions? client)> TryGetClientAsync(Guid guid);
    Task<List<ClientSessions>> ListAllClients();
    Task Clear();
}