using Helios.Database.Attributes;
using Fleck;
using System.Text.Json; 

namespace Helios.Database.Tables.XMPP;

[Entity("client_sessions")]
public class ClientSessions : BaseTable
{
    [Column("socketId")]
    public Guid SocketId { get; set; }
    
    [Column("accountId")]
    public string AccountId { get; set; }
    
    [Column("displayName")]
    public string DisplayName { get; set; }
    
    [Column("token")]
    public string Token { get; set; }
    
    [Column("jid")]
    public string Jid { get; set; }
    
    [Column("resource")]
    public string Resource { get; set; }
    
    [Column("lastPresenceUpdate")]
    public string LastPresenceUpdate { get; set; } = JsonSerializer.Serialize(new LastPresenceUpdate());
    
    [Column("isLoggedIn")]
    public bool IsLoggedIn { get; set; }
    
    [Column("isAuthenticated")]
    public bool IsAuthenticated { get; set; }
}

public class LastPresenceUpdate
{
    public bool IsAway { get; set; } = false;
    public string StatusString { get; set; } = "{}";
}