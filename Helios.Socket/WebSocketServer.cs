using System.Text.Json;
using Fleck;
using Helios.Database.Tables.XMPP;
using Helios.Socket.Classes;
using Helios.Socket.Events;
using Helios.Socket.Interfaces;
using Helios.Socket.Services;
using ErrorEventArgs = System.IO.ErrorEventArgs;
using IWebSocketServer = Helios.Socket.Interfaces.IWebSocketServer;

namespace Helios.Socket;

public class WebSocketServer : IWebSocketServer, IDisposable
{
    private Fleck.IWebSocketServer _server;
    private readonly IClientManager _clientManager;
    private readonly WebSocketConfiguration _configuration;
    private readonly MessageHandler _messageHandler;
    private bool _disposed;
    public bool IsRunning { get; private set; }
    
    public event EventHandler<ClientConnectedEventArgs> ClientConnected;
    public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;
    public event EventHandler<MessageReceivedEventArgs> MessageReceived;
    public event EventHandler<Events.ErrorEventArgs> ErrorOccurred;

    public WebSocketServer(WebSocketConfiguration configuration = null)
    {
        _configuration = configuration ?? new WebSocketConfiguration();
        _clientManager = new ClientManager();
        _messageHandler = new MessageHandler(_clientManager);
        
        IsRunning = false;
    }
    
    public void Start()
    {
        if (IsRunning)
            return;

        try
        {
            Logger.Info($"Starting WebSocket server on {_configuration.ServerUrl}");

            if (_configuration.EnableLogging)
            {
                FleckLog.Level = LogLevel.Debug;
                FleckLog.LogAction = (level, message, ex) =>
                {
                    var fullMessage = $"[Fleck] {message} {ex?.Message ?? string.Empty}";

                    switch (level)
                    {
                        case LogLevel.Debug:
                            Logger.Debug(fullMessage);
                            break;
                        case LogLevel.Info:
                            Logger.Info(fullMessage);
                            break;
                        case LogLevel.Warn:
                            Logger.Warn(fullMessage);
                            break;
                        case LogLevel.Error:
                            Logger.Error(fullMessage, ex);
                            break;
                        default:
                            Logger.Info(fullMessage);
                            break;
                    }
                };
            }  
            else
            {
                FleckLog.Level = LogLevel.Error;
            }
            
            var server = new Fleck.WebSocketServer(_configuration.ServerUrl);
            server.RestartAfterListenError = _configuration.RestartAfterListenError;
            server.Start(async socket =>
            {
                socket.OnOpen = async () => await HandleClientConnected(socket);
                socket.OnClose = async () => await HandleClientDisconnected(socket);
                socket.OnMessage = async message => await HandleMessageReceived(socket, message);
                socket.OnError = async ex => await HandleError(socket, ex);
            });

            _server = server;
            IsRunning = true;
            
            Logger.Info("WebSocket server started successfully");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to start WebSocket server: {ex.Message}");
            OnErrorOccurred(new Events.ErrorEventArgs
            {
                Error = ex,
                ErrorSource = "Server initialization"
            });
            throw;        
        }
    }
    
    public async Task Stop()
    {
        if (!IsRunning)
            return;

        try
        {
            Logger.Info("Stopping WebSocket server");

            var clients = await _clientManager.ListAllClients();
            foreach (var client in clients)
            {
                try
                {
                    Globals._socketConnections[client.SocketId].Close();
                }
                catch (Exception ex)
                {
                   Logger.Warn($"Error closing client {client.AccountId}: {ex.Message}");
                }
            }

            _clientManager.Clear();
                
            (_server as IDisposable)?.Dispose();
            IsRunning = false;
            Logger.Info("WebSocket server stopped successfully");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error stopping WebSocket server: {ex.Message}");
            OnErrorOccurred(new Events.ErrorEventArgs
            {
                Error = ex,
                ErrorSource = "Server shutdown"
            });
            throw;
        }
    }
    
    private async Task HandleClientConnected(IWebSocketConnection socket)
    {
        try
        {
            var newSession = new ClientSessions
            {
                SocketId = socket.ConnectionInfo.Id
            };
            await _clientManager.AddClient(newSession, socket);
            
            OnClientConnected(new ClientConnectedEventArgs { Client = newSession });
        }
        catch (Exception ex)
        {
            Logger.Error($"Error handling client connection: {ex.Message}");
            OnErrorOccurred(new Events.ErrorEventArgs
            {
                Client = null,
                Error = ex,
                ErrorSource = "Client connection handler"
            });
        }
    }
    
    private async Task HandleClientDisconnected(IWebSocketConnection socket)
    {
        try
        {
            var (success, client) = await _clientManager.TryGetClientAsync(socket.ConnectionInfo.Id);
            if (success && client != null)
            {
                await _clientManager.RemoveClient(socket.ConnectionInfo.Id);
                OnClientDisconnected(new ClientDisconnectedEventArgs { Client = client });
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error handling client disconnection: {ex.Message}");
            OnErrorOccurred(new Events.ErrorEventArgs
            {
                Client = new ClientSessions(),
                Error = ex,
                ErrorSource = "Client disconnection handler"
            });
        }
    }
    
    private async Task HandleMessageReceived(IWebSocketConnection socket, string message)
    {
        try
        {
            var clientSession = await _clientManager.Clients.FindAsync(
                new ClientSessions { SocketId = socket.ConnectionInfo.Id });
            
            if (clientSession == null) return;
            
            OnMessageReceived(new MessageReceivedEventArgs
            {
                Client = clientSession,
                Message = message
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"Error handling message: {ex.Message}");
            OnErrorOccurred(new Events.ErrorEventArgs
            {
                Client = new ClientSessions(),
                Error = ex,
                ErrorSource = "Message handler"
            });
        }
    }

    private async Task HandleError(IWebSocketConnection socket, Exception ex)
    {
        try
        {
            var clientSession = await _clientManager.Clients.FindAsync(new ClientSessions { SocketId = socket.ConnectionInfo.Id });
            if (clientSession != null)
            {
                Logger.Error($"Error from client {clientSession.SocketId}: {ex.Message}");
                OnErrorOccurred(new Events.ErrorEventArgs
                {
                    Client = clientSession,
                    Error = ex,
                    ErrorSource = "Client connection"
                });
                
                if (ex.Message.Contains("An existing connection was forcibly closed by the remote host"))
                {
                    Logger.Warn($"Removing forcibly closed client: {clientSession.SocketId}");
                    await _clientManager.RemoveClient(clientSession.SocketId);
                    OnClientDisconnected(new ClientDisconnectedEventArgs { Client = clientSession });
                }
            }
        }
        catch (Exception handlerEx)
        {
            if (!handlerEx.Message.Contains("An existing connection was forcibly closed by the remote host"))
            {
                Logger.Error($"Error in error handler: {handlerEx.Message}");
                OnErrorOccurred(new Events.ErrorEventArgs
                {
                    Error = handlerEx,
                    ErrorSource = "Error handler"
                });
            }
        }
    }
    
    public async Task SendToClient(Guid socketId, string message)
    {
        await _messageHandler.SendToClient(socketId, message);
    }

    public async Task BroadcastMessage(string message)
    {
        await _messageHandler.BroadcastMessage(message);
    }

    public async Task BroadcastMessageExcept(Guid excludedSocketId, string message)
    {
        await _messageHandler.BroadcastMessageExcept(excludedSocketId, message);
    }

    public bool TryParseXmppMessage(string message, out XmppMessage xmppMessage)
    {
        return _messageHandler.TryParseXmppMessage(message, out xmppMessage);
    }
    
    protected virtual void OnClientConnected(Events.ClientConnectedEventArgs e)
    {
        ClientConnected?.Invoke(this, e);
    }
   

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            if (IsRunning)
            {
                Stop();
            }
        }

        _disposed = true;
    }

    ~WebSocketServer()
    {
        Dispose(false);
    }
    
    
    protected virtual void OnClientDisconnected(Events.ClientDisconnectedEventArgs e)
    {
        ClientDisconnected?.Invoke(this, e);
    }

    protected virtual void OnMessageReceived(Events.MessageReceivedEventArgs e)
    {
        MessageReceived?.Invoke(this, e);
    }

    protected virtual void OnErrorOccurred(Events.ErrorEventArgs e)
    {
        ErrorOccurred?.Invoke(this, e);
    }
}