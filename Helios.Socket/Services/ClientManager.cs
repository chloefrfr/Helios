using System.Text.Json;
using Fleck;
using Helios.Database.Repository;
using Helios.Database.Tables.XMPP;
using Helios.Socket.Interfaces;

namespace Helios.Socket.Services;

public class ClientManager : IClientManager
{
    public Repository<ClientSessions> Clients { get; set; } = WebSocketConfiguration.repositoryPool.GetRepository<ClientSessions>();

    public async Task AddClient(ClientSessions client, IWebSocketConnection socket)
    {
        // if (client.Socket == null)
        //     throw new ArgumentNullException(nameof(client.Socket));

        await Clients.SaveAsync(client).ConfigureAwait(false);
        Globals._socketConnections[client.SocketId] = socket;
    }

    public async Task RemoveClient(string accountId)
    {
        await Clients.DeleteAsync(new ClientSessions { AccountId = accountId }).ConfigureAwait(false);
    }
    
    public async Task RemoveClient(Guid guid)
    {
        await Clients.DeleteAsync(new ClientSessions { SocketId = guid }).ConfigureAwait(false);
    }

    public async Task RemoveClient(IWebSocketConnection connection)
    {
        Globals._socketConnections.Remove(connection.ConnectionInfo.Id);
    }

    public async Task<(bool success, ClientSessions? client)> TryGetClientAsync(string accountId)
    {
        var client = await Clients.FindAsync(new ClientSessions { AccountId = accountId });
        return (client != null, client);
    }
    
    public async Task<(bool success, ClientSessions? client)> TryGetClientAsync(Guid guid)
    {
        var client = await Clients.FindAsync(new ClientSessions { SocketId = guid });
        return (client != null, client);
    }

    public async Task<List<ClientSessions>> ListAllClients()
    {
        return await Clients.FindAllByTableAsync();
    }

    public async Task Clear()
    {
        await Clients.DeleteAsync(new ClientSessions()); // TODO: Add DeleteAllByTableAsync
    }
}