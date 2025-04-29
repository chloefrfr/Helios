using System.Diagnostics;
using Helios.Database.Tables.XMPP;
using Helios.Socket.Classes;
using Helios.Socket.Interfaces;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Helios.Socket.Admin;

public class AdminCommandService
{
    private readonly IClientManager _clientManager;
    private readonly AdminAuthService _authService;
    
    public AdminCommandService(IClientManager clientManager, AdminAuthService authService)
    {
        _clientManager = clientManager;
        _authService = authService;
    }

    public async Task<object> HandleAdminCommand(ClientSessions client, AdminMessage adminMessage)
    {
        var clientSessionsRepo = WebSocketConfiguration.repositoryPool.GetRepository<ClientSessions>();
        // var clientSession = await clientSessionsRepo.FindAsync(new ClientSessions { SocketId = client.SocketId });
        
        if (adminMessage.Type == "login")
        {
            var payload = adminMessage.Payload?.GetProperty("credentials");
            var username = payload?.GetProperty("username").GetString();
            var password = payload?.GetProperty("password").GetString();

            if (username == null || password == null)
                return new { success = false, error = "Invalid credentials format" };

            if (username != "admin" || !_authService.VerifyPassword(password))
                return new { success = false, error = "Invalid credentials" };

            var token = _authService.GenerateAdminToken();

            client.IsAdmin = true;
            client.IsAuthenticated = true;
            await clientSessionsRepo.UpdateAsync(client);

            return new { success = true, token };
        }

        if (!_authService.ValidateAdminToken(adminMessage.Token))
        {
            return new { success = false, error = "Unauthorized" };
        }

        switch (adminMessage.Type)
        {
            case "listUsers":
                var clients = await _clientManager.ListAllClients();
                var clientList = clients.Select(c => new
                {
                    c.SocketId,
                    c.AccountId,
                    c.DisplayName,
                    c.IsAdmin,
                    c.IsAuthenticated,
                }).ToList();

                return new { success = true, clients = clientList };

            case "kickUser":
                var socketId = adminMessage.Payload?.GetProperty("socketId").GetGuid();
                if (socketId == null || socketId == Guid.Empty)
                    return new { success = false, error = "Invalid socketId" };

                var clientToKick =
                    await _clientManager.Clients.FindAsync(new ClientSessions { SocketId = socketId.Value });
                if (clientToKick == null)
                    return new { success = false, error = "Client not found" };

                if (clientToKick.IsAdmin && clientToKick.SocketId != client.SocketId)
                    return new { success = false, error = "Cannot kick another admin" };

                if (Globals._socketConnections.TryGetValue(socketId.Value, out var connection))
                {
                    connection.Close();
                }

                await _clientManager.RemoveClient(socketId.Value);

                return new { success = true, message = $"User kicked: {socketId}" };

            case "broadcast":
                var message = adminMessage.Payload?.GetProperty("message").GetString();
                if (string.IsNullOrEmpty(message))
                    return new { success = false, error = "Message is required" };

                var broadcastMsg = new XmppMessage
                {
                    Type = "system",
                    From = "admin",
                    To = "all",
                    RawContent = message
                };

                var json = JsonSerializer.Serialize(broadcastMsg.ToString());
                var allClients = await _clientManager.ListAllClients();
                foreach (var c in allClients)
                {
                    if (Globals._socketConnections.TryGetValue(c.SocketId, out var conn))
                    {
                        await conn.Send(json);
                    }
                }

                return new { success = true, message = "Broadcast sent" };

            case "serverStats":
                var stats = new
                {
                    totalConnections = (await _clientManager.ListAllClients()).Count,
                    adminConnections = (await _clientManager.ListAllClients()).Count(c => c.IsAdmin),
                    authenticatedUsers = (await _clientManager.ListAllClients()).Count(c => c.IsAuthenticated),
                    uptime =
                        (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).ToString(
                            @"dd\.hh\:mm\:ss")
                };

                return new { success = true, stats };

            case "logout":
                _authService.RevokeToken(adminMessage.Token);

                client.IsAdmin = false;
                client.Token = null;
                await clientSessionsRepo.UpdateAsync(client);

                return new { success = true, message = "Logged out successfully" };

            default:
                return new { success = false, error = "Unknown command" };
        }
    }
}