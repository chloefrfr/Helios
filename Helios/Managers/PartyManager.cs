using System.Collections.Concurrent;
using System.Xml.Linq;
using Helios.Configuration;
using Helios.Database.Repository;
using Helios.Database.Tables.Fortnite;
using Helios.Database.Tables.XMPP;
using Helios.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Helios.Managers;

public class PartyManager : IDisposable
{
    private readonly Func<Repository<Parties>> _pRepo = Constants.repositoryPool.Repo<Parties>(false);
    private readonly Func<Repository<ClientSessions>> _sessionsRepo = Constants.repositoryPool.Repo<ClientSessions>(false);
    private ConcurrentDictionary<string, Parties> _parties = new();
    private ConcurrentDictionary<string, ClientSessions> _clientSessions = new();
    private Timer _cleanupTimer;
    private bool _disposed;
    private bool _initialized = false;
    private DateTime _lastCleanupTime;
    
    private const int CLEANUP_INTERVAL_SECONDS = 15;
    
    public PartyManager()
    {
        _cleanupTimer = new Timer(CleanupAndRefreshData, null, TimeSpan.Zero, TimeSpan.FromSeconds(CLEANUP_INTERVAL_SECONDS));
        _lastCleanupTime = DateTime.Now;
        Logger.Info($"PartyManager initialized.");
    }

    public async Task InitializeAsync()
    {
        try
        {
            await RefreshDataFromDatabase();
            _initialized = true;
            Logger.Info("PartyManager initialized successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error initializing PartyManager: {ex}");
            throw;
        }
    }

    private async Task RefreshDataFromDatabase()
    {
        try
        {
            var parties = await _pRepo().FindAllByTableAsync(useCache:false);
            var distinctParties = parties.DistinctBy(p => p.PartyId).ToList();
            
            if (distinctParties.Count < parties.Count)
            {
                Logger.Warn($"Filtered out {parties.Count - distinctParties.Count} duplicate PartyId entries");
            }
            
            var updatedParties = new ConcurrentDictionary<string, Parties>(
                distinctParties.Select(p => new KeyValuePair<string, Parties>(p.PartyId, p)));
                
            _parties = updatedParties;
            
            var sessions = await _sessionsRepo().FindAllByTableAsync(useCache:false);
            var distinctSessions = sessions.DistinctBy(s => s.AccountId).ToList();
            
            if (distinctSessions.Count < sessions.Count)
            {
                Logger.Warn($"Filtered out {sessions.Count - distinctSessions.Count} duplicate AccountId entries");
            }
            
            var updatedSessions = new ConcurrentDictionary<string, ClientSessions>(
                distinctSessions.Select(s => new KeyValuePair<string, ClientSessions>(s.AccountId, s)));
                
            _clientSessions = updatedSessions;
            
            Logger.Info($"Data refreshed from database: {_parties.Count} parties and {_clientSessions.Count} sessions loaded.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error refreshing data from database: {ex}");
        }
    }

    private async void CleanupAndRefreshData(object? state)
    {
        if (_disposed || !_initialized) return;

        TimeSpan elapsed = DateTime.Now - _lastCleanupTime;
        _lastCleanupTime = DateTime.Now;
        
        await RefreshDataFromDatabase();
        
        var partiesToRemove = new List<Parties>();

        foreach (var party in _parties.Values)
        {
            try
            {
                var partyMembers = JsonSerializer.Deserialize<List<PartyMember>>(party.Members);

                if (partyMembers == null)
                {
                    Logger.Warn($"Failed to deserialize members for party {party.PartyId}");
                    partiesToRemove.Add(party);
                    continue;
                }
                
                bool anyActiveMembers = partyMembers.Any(member =>
                {
                    bool isActive = _clientSessions.ContainsKey(member.AccountId);
                    return isActive;
                });

                if (!anyActiveMembers)
                {
                    partiesToRemove.Add(party);
                }
            }
            catch (JsonException ex)
            {
                Logger.Error($"Error deserializing party {party.PartyId}: {ex.Message}");
                partiesToRemove.Add(party);
            }
        }
        
        foreach (var party in partiesToRemove)
        {
            if (_parties.TryRemove(party.PartyId, out _))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await _pRepo().DeleteByColumnAsync("partyid", party.PartyId);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to delete party {party.PartyId}: {ex.Message}");
                    }
                });
            }
        }
        
        EnsureTimerIsRunning();
    }
    
    private void EnsureTimerIsRunning()
    {
        try
        {
            _cleanupTimer?.Dispose();
            
            _cleanupTimer = new Timer(CleanupAndRefreshData, null, 
                TimeSpan.FromSeconds(CLEANUP_INTERVAL_SECONDS), 
                TimeSpan.FromSeconds(CLEANUP_INTERVAL_SECONDS));
        }
        catch (Exception ex)
        {
            Logger.Error($"Error resetting cleanup timer: {ex.Message}");
        }
    }
    
    public static string CreateXmlMessage(string jid, string body)
    {
        var xml = new XElement(XNamespace.Get("jabber:client") + "message",
            new XAttribute("xmlns", "jabber:client"),
            new XAttribute("to", jid),
            new XAttribute("from", "xmpp-admin@prod.ol.epicgames.com"),
            new XElement("body", body)
        );
        return xml.ToString(SaveOptions.DisableFormatting);
    }
    
    private static object ExtractJTokenValue(JToken token)
    {
        switch (token.Type)
        {
            case JTokenType.String:
                return token.Value<string>();
            case JTokenType.Integer:
                return token.Value<long>();
            case JTokenType.Float:
                return token.Value<double>();
            case JTokenType.Boolean:
                return token.Value<bool>();
            case JTokenType.Object:
                return ConvertJTokenToObject(token);
            case JTokenType.Array:
                return token.Select(ExtractJTokenValue).ToList();
            case JTokenType.Null:
                return null;
            default:
                return token.ToString();
        }
    }

    public static Dictionary<string, object> ConvertJTokenToObject(JToken token)
    {
        var result = new Dictionary<string, object>();
    
        if (token is JObject jObject)
        {
            foreach (var property in jObject.Properties())
            {
                result[property.Name] = ExtractJTokenValue(property.Value);
            }
        }
    
        return result;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer?.Dispose();
            _disposed = true;
            Logger.Info("PartyManager disposed");
        }
    }
}