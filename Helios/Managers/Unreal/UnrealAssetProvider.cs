using System.Diagnostics;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using Helios.Configuration;
using Helios.Utilities;
using Helios.Utilities.Caching;

namespace Helios.Managers.Unreal;

public class UnrealAssetProvider : IDisposable
{
    private readonly SemaphoreSlim _initializationLock = new  SemaphoreSlim(1, 1);
    private bool _isInitialized;
    private bool _isDisposed;

    private const string COSMETICS_CACHE_KEY = "cosmetic_assets";
    
    public DefaultFileProvider FileProvider { get; private set; }

    public UnrealAssetProvider()
    {
        
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            Logger.Debug($"Provider already initialized, skipping initialization");
            return;
        }
        
        await _initializationLock.WaitAsync(cancellationToken);

        try
        {
            if (_isInitialized)
                return;
            
            Logger.Info($"Initializing provider with game directory: {Constants.config.GameDirectory}");
            ValidateGameDirectory(Constants.config.GameDirectory);
            
            FileProvider = new DefaultFileProvider(
                Constants.config.GameDirectory,
                SearchOption.AllDirectories,
                true,
                new VersionContainer(EGame.GAME_UE4_LATEST));
            
            await Task.WhenAll(
                Task.Run(() => FileProvider.Initialize(), cancellationToken)
                // Task.Run(() => FileProvider.LoadLocalization(), cancellationToken)
            );

            var version = FVersionConverter.Convert(Constants.config.CurrentVersion);
            var aesKey = FAesProvider.GetAesKey(version);
            
            if (aesKey.Key == null)
            {
                throw new InvalidOperationException($"Failed to GET AES key for version {Constants.config.CurrentVersion}");
            }
            
            var aesKeys = new List<KeyValuePair<FGuid, FAesKey>>
            {
                new KeyValuePair<FGuid, FAesKey>(new FGuid(), new FAesKey(aesKey.Key))
            };

            await FileProvider.SubmitKeysAsync(aesKeys, cancellationToken);
            
            string keyTxt = FileProvider.Keys.Count == 1 ? "key" : "keys";
            Logger.Info($"Provider initialized with " +
                        $"with {FileProvider.Keys.Count} {keyTxt}. Version {Constants.config.CurrentVersion}");
            
            foreach (var vfs in FileProvider.MountedVfs)
            {
                Logger.Info($"Successfully mounted file '{vfs.Name}'");
            }
            
            _isInitialized = true;
        }   
        catch (FileNotFoundException ex)
        {
            Logger.Error($"File not found: {ex.FileName}");
            throw new InvalidOperationException($"Required file not found: {ex.FileName}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.Error($"Access denied to game directory: {ex.Message}");
            throw new InvalidOperationException("Access denied to game directory. Check permissions.", ex);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to initialize provider: {ex.Message}");
            throw new InvalidOperationException("Failed to initialize provider. See inner exception for details.", ex);
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private void ValidateGameDirectory(string gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory))
        {
            throw new ArgumentException("Game directory cannot be null or empty", nameof(gameDirectory));
        }

        if (!Directory.Exists(gameDirectory))
        {
            throw new DirectoryNotFoundException($"Game directory not found: {gameDirectory}");
        }

        try
        {
            Directory.GetFiles(gameDirectory, "*", SearchOption.TopDirectoryOnly);
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException($"Access denied to game directory: {gameDirectory}");
        }
    }
    
     public async Task<IReadOnlyList<string>> LoadAssetsFromPathAsync(
            string path,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            
            if (string.IsNullOrWhiteSpace(path))
            {
                Logger.Warn("Provided path is null or empty");
                return Array.Empty<string>();
            }

            string cacheKey = $"assets_path_{path}";
            
            if (forceRefresh)
            {
                HeliosFastCache.Remove(cacheKey);
            }

            return await HeliosFastCache.GetOrAddAsync<IReadOnlyList<string>>(
                cacheKey,
                async () =>
                {
                    try
                    {
                        var stopwatch = Stopwatch.StartNew();
                        
                        var assetFiles = await Task.Run(() =>
                        {
                            return FileProvider.Files
                                .AsParallel()
                                .Where(file => file.Key.StartsWith(path, StringComparison.OrdinalIgnoreCase) && 
                                              file.Key.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
                                .Select(file => file.Key)
                                .ToList();
                        }, cancellationToken);

                        stopwatch.Stop();
                        
                        Logger.Debug($"Loaded {assetFiles.Count} assets from path '{path}' in {stopwatch.ElapsedMilliseconds}ms");
                        
                        return assetFiles;
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.Info("Asset loading operation was canceled");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error loading assets from path '{path}': {ex}");
                        return Array.Empty<string>();
                    }
                },
                TimeSpan.FromHours(1) 
            );
        }
    
    public async Task<IReadOnlyList<string>> LoadAllCosmeticsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        const string cosmeticsPath = "FortniteGame/Content/Athena/Items/Cosmetics";
            
        if (forceRefresh)
        {
            HeliosFastCache.Remove(COSMETICS_CACHE_KEY);
        }

        return await HeliosFastCache.GetOrAddAsync<IReadOnlyList<string>>(
            COSMETICS_CACHE_KEY,
            async () => await LoadAssetsFromPathAsync(cosmeticsPath, false, cancellationToken),
            TimeSpan.FromHours(2) 
        );
    }
    
    private void EnsureInitialized()
    {
        if (!_isInitialized || FileProvider == null)
        {
            throw new InvalidOperationException("UnrealAssetProvider is not initialized. Call InitializeAsync first.");
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _initializationLock?.Dispose();
        FileProvider?.Dispose();
            
        _isDisposed = true;
    }
}