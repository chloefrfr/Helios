using System.Text.Json;
using Fleck;
using Helios.Database.Repository;
using Helios.Database.Tables.XMPP;
using Helios.Socket.Interfaces;

namespace Helios.Socket.Services;

public class ClientManager : IClientManager
{
    public Repository<ClientSessions> Clients { get; set; } = WebSocketConfiguration.repositoryPool.For<ClientSessions>();

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
    
    public async Task RemoveClient(Guid socketId)
    {
        var session = await Clients.FindAsync(new ClientSessions { SocketId = socketId });
        if (session != null)
        {
            Globals._socketConnections.Remove(session.SocketId);
            await Clients.DeleteAsync(new ClientSessions { SocketId = socketId });
            
            Logger.Info($"Removed client {session.SocketId}");
        }
    }

    public async Task RemoveClient(IWebSocketConnection connection)
    {
        await Clients.DeleteAsync(new ClientSessions { SocketId = connection.ConnectionInfo.Id }).ConfigureAwait(false);
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

    public async Task<bool> ClientExists(Guid guid)
    {
        var client = await Clients.FindAsync(new ClientSessions { SocketId = guid });
        return client != null;
    }
}