using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Helios.Classes.MCP;
using Helios.Configuration;
using Helios.Database.Tables.Profiles;
using Helios.Utilities;
using Helios.Utilities.Profile;
using Helios.Utilities.Caching;

namespace Helios.Managers;

public static class ProfileManager
{
    private static readonly TimeSpan _defaultCacheExpiration = TimeSpan.FromMinutes(15);
    private static readonly Dictionary<string, Func<string, List<Items>>> _profileTypeInitializers = new();
    private static bool _initializerRegistered = false;
    
    static ProfileManager()
    {
        RegisterDefaultProfileInitializers();
    }
    
    private static void RegisterDefaultProfileInitializers()
    {
        if (_initializerRegistered) return;
        
        _profileTypeInitializers.Add("athena", CreateAthenaItems);
        _profileTypeInitializers.Add("common_core", CreateCommonCoreItems);
        
        _initializerRegistered = true;
    }
    
    public static async Task<Profiles> CreateProfileAsync(string type, string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(type))
        {
            Logger.Error($"Invalid accountId or type: {accountId}, {type}");
            return null;
        }

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var profileRepository = Constants.repositoryPool.GetRepository<Profiles>();
            var itemsRepository = Constants.repositoryPool.GetRepository<Items>();
            
            string cacheKey = $"profile_{accountId}_{type}";
            if (HeliosFastCache.TryGet<Profiles>(cacheKey, out var cachedProfile))
            {
                return cachedProfile;
            }
            
            var newProfile = new Profiles
            {
                AccountId = accountId,
                ProfileId = type,
                Revision = 0,
            };

            await profileRepository.SaveAsync(newProfile);
            
            List<Items> itemsToCreate = new List<Items>();
            
            if (_profileTypeInitializers.TryGetValue(type, out var initializer))
            {
                itemsToCreate = initializer(accountId);
            }
            else
            {
                Logger.Warn($"No initializer found for profile type: {type}");
            }
            
            if (itemsToCreate.Count > 0)
            {
                await itemsRepository.BulkInsertAsync(itemsToCreate);
            }
            
            HeliosFastCache.Set(cacheKey, newProfile, _defaultCacheExpiration);
            
            stopwatch.Stop();
            Logger.Debug($"CreateProfileAsync took {stopwatch.ElapsedMilliseconds}ms");
            
            return newProfile;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.Error($"Failed to create profile for account {accountId} with type {type}: {ex.Message}");
            Logger.Debug($"Stack trace: {ex.StackTrace}");
            
            return new Profiles();
        }
    }
    
    public static async Task<Profiles> GetProfileAsync(string type, string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(type))
        {
            Logger.Error($"Invalid accountId or type: {accountId}, {type}");
            return null;
        }
        
        string cacheKey = $"profile_{accountId}_{type}";
        
        return await HeliosFastCache.GetOrAddAsync(cacheKey, async () => 
        {
            var profileRepository = Constants.repositoryPool.GetRepository<Profiles>();
            return await profileRepository.FindAsync(new Profiles
            {
                AccountId = accountId,
                ProfileId = type
            });
        }, _defaultCacheExpiration);
    }

    public static async Task<bool> UpdateProfileAsync(Profiles profile)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.AccountId) || string.IsNullOrWhiteSpace(profile.ProfileId))
        {
            Logger.Error("Invalid profile data for update");
            return false;
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var profileRepository = Constants.repositoryPool.GetRepository<Profiles>();
            
            await profileRepository.UpdateAsync(new Profiles
            {
                Revision = profile.Revision + 1
            });
            
            string cacheKey = $"profile_{profile.AccountId}_{profile.ProfileId}";
            HeliosFastCache.Set(cacheKey, profile, _defaultCacheExpiration);
            
            stopwatch.Stop();
            Logger.Debug($"UpdateProfileAsync took {stopwatch.ElapsedMilliseconds}ms");
            
            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.Error($"Failed to update profile for account {profile.AccountId} with type {profile.ProfileId}: {ex.Message}");
            return false;
        }
    }
    
    public static void RegisterProfileTypeInitializer(string profileType, Func<string, List<Items>> initializer)
    {
        if (string.IsNullOrWhiteSpace(profileType))
        {
            throw new ArgumentException("Profile type cannot be null or empty", nameof(profileType));
        }
        
        if (initializer == null)
        {
            throw new ArgumentNullException(nameof(initializer));
        }
        
        _profileTypeInitializers[profileType] = initializer;
    }

    private static List<Items> CreateCommonCoreItems(string accountId)
    {
        return new List<Items>
        {
            ProfileItemCreator.CreateCCItem("common_core", accountId, "Currency:MtxPurchased")
        };
    }
    
    private static List<Items> CreateAthenaItems(string accountId)
    {
        var items = new List<Items>(capacity: 40);
        
        items.Add(ProfileItemCreator.CreateItem("athena", accountId, "AthenaPickaxe:DefaultPickaxe"));
        items.Add(ProfileItemCreator.CreateItem("athena", accountId, "AthenaGlider:DefaultGlider"));
        items.Add(ProfileItemCreator.CreateItem("athena", accountId, "AthenaDance:EID_DanceMoves"));
        items.Add(ProfileItemCreator.CreateItem("athena", accountId, "AthenaCharacter:CID_001_Athena_Commando_F_Default"));
            
        var statItems = new Dictionary<string, object>
        {
            ["use_random_loadout"] = false,
            ["past_seasons"] = new List<PastSeasons>(),
            ["season_match_boost"] = 0,
            ["loadouts"] = new List<string>(),
            ["mfa_reward_claimed"] = false,
            ["rested_xp_overflow"] = 0,
            ["current_mtx_platform"] = "Epic",
            ["last_xp_interaction"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            ["quest_manager"] = new QuestManager
            {
                DailyLoginInterval = "0001-01-01T00:00:00.000Z",
                DailyQuestRerolls = 1,
                QuestPoolStats = new QuestPoolStats()
            },
            ["book_level"] = 1,
            ["season_num"] = 13,
            ["book_xp"] = 0,
            ["creative_dynamic_xp"] = new Dictionary<string, int>(),
            ["party_assist_quest"] = "",
            ["pinned_quest"] = "",
            ["vote_data"] = new VoteData
            {
                ElectionId = "",
                VoteHistory = new Dictionary<string, object>(),
                VotesRemaining = 0,
                LastVoteGranted = ""
            },
            ["lifetime_wins"] = 0,
            ["book_purchased"] = false,
            ["rested_xp_exchange"] = 1,
            ["level"] = 1,
            ["rested_xp"] = 2500,
            ["rested_xp_mult"] = 4.4,
            ["accountLevel"] = 1,
            ["rested_xp_cumulative"] = 52500,
            ["xp"] = 0,
            ["battlestars"] = 0,
            ["battlestars_season_total"] = 0,
            ["season_friend_match_boost"] = 0,
            ["active_loadout_index"] = 0,
            ["purchased_bp_offers"] = new List<string>(),
            ["purchased_battle_pass_tier_offers"] = new List<string>(),
            ["last_match_end_datetime"] = "",
            ["mtx_purchase_history_copy"] = new List<object>(),
            ["last_applied_loadout"] = "",
            ["favorite_musicpack"] = "",
            ["banner_icon"] = "BRSeason01",
            ["favorite_character"] = "AthenaCharacter:CID_001_Athena_Commando_F_Default",
            ["favorite_itemwraps"] = new List<string> { "", "", "", "", "", "", "" },
            ["favorite_skydivecontrail"] = "",
            ["favorite_pickaxe"] = "AthenaPickaxe:DefaultPickaxe",
            ["favorite_glider"] = "AthenaGlider:DefaultGlider",
            ["favorite_backpack"] = "",
            ["favorite_dance"] = new List<string> { "", "", "", "", "", "", "" },
            ["favorite_loadingscreen"] = "",
            ["banner_color"] = "DefaultColor1",
        };

        foreach (var (key, value) in statItems)
        {
            items.Add(ProfileItemCreator.CreateStatItem("athena", accountId, key, value));
        }

        return items;
    }
}