using System.Diagnostics;
using CUE4Parse.Compression;
using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using Helios.Classes.HTTP;
using Helios.Configuration;
using Helios.Utilities;
using Helios.Utilities.Caching;
using Newtonsoft.Json;

namespace Helios.Managers.Unreal;

public class UnrealAssetProvider : IDisposable
{
    private readonly SemaphoreSlim _initializationLock = new SemaphoreSlim(1, 1);
    private bool _isInitialized;
    private bool _isDisposed;
    
    public DefaultFileProvider FileProvider { get; private set; }

    public UnrealAssetProvider()
    {
        string DataPath = $"{Directory.GetCurrentDirectory()}\\.data";
        if (!Directory.Exists(DataPath))
            Directory.CreateDirectory(DataPath);
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
                SearchOption.TopDirectoryOnly,
                true,
                new VersionContainer(EGame.GAME_UE4_LATEST));
            
           FileProvider.Initialize();

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
            string DataPath = $"{Directory.GetCurrentDirectory()}\\.data";
            
            var oodlePath = Path.Combine(DataPath, OodleHelper.OODLE_DLL_NAME);
            if (File.Exists(OodleHelper.OODLE_DLL_NAME))
            {
                File.Move(OodleHelper.OODLE_DLL_NAME, oodlePath, true);
            }
            else if (!File.Exists(oodlePath))
            {
                await OodleHelper.DownloadOodleDllAsync(oodlePath);
            }

            OodleHelper.Initialize(oodlePath);

            var mappings = await GetMappings();
            FileProvider.MappingsContainer = mappings;
            
            string keyTxt = FileProvider.Keys.Count == 1 ? "key" : "keys";
            Logger.Info($"Provider initialized with " +
                        $"with {FileProvider.Keys.Count} {keyTxt}. Version {Constants.config.CurrentVersion}");
            
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
    
    private async Task<FileUsmapTypeMappingsProvider> GetMappings()
    {
        var mappingsData = JsonConvert.DeserializeObject<List<MappingsResponse>>(await new HttpClient().GetStringAsync("https://fortnitecentral.genxgames.gg/api/v1/mappings"))[0];
        string DataPath = $"{Directory.GetCurrentDirectory()}\\.data";
        
        var path = Path.Combine(DataPath, mappingsData.FileName);
        if (!File.Exists(path))
        {
            Logger.Info($"Cant find latest mappings, Downloading {mappingsData.Url}");

            var bytes = await new HttpClient().GetByteArrayAsync(mappingsData.Url);
            await File.WriteAllBytesAsync(path, bytes);
        }

        var latestUsmapInfo = new DirectoryInfo(DataPath).GetFiles("*_oo.usmap").FirstOrDefault(x => x.Name == mappingsData.FileName);

        Logger.Info(latestUsmapInfo != null ? $"Mappings Pulled from file: {latestUsmapInfo.Name}" : "Could not find mappings!");

        return new FileUsmapTypeMappingsProvider(latestUsmapInfo!.FullName);
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

    public async Task<List<string>> LoadAllCosmeticsAsync(CancellationToken cancellationToken)
    {
        if (FileProvider == null) return new List<string>();
        const string cosmeticsPath = "FortniteGame/Content/Athena/Items/Cosmetics";

        if (HeliosFastCache.TryGet<List<string>>(cosmeticsPath, out var cachedCosmetics))
            return cachedCosmetics;

        var cosmetics = new List<string>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await foreach (var batch in HandleFilesInBatchesAsync(cosmeticsPath)
                               .WithCancellation(cancellationToken))
            {
                cosmetics.AddRange(batch);
            }

            HeliosFastCache.Set(cosmeticsPath, cosmetics);
            stopwatch.Stop();
            Logger.Info($"Loaded {cosmetics.Count} cosmetics in {stopwatch.ElapsedMilliseconds} ms.");

            return cosmetics;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error loading cosmetics: {ex.Message}");
            return new List<string>();
        }
    }

    private async IAsyncEnumerable<List<string>> HandleFilesInBatchesAsync(string pathPrefix)
    {
        int BatchSize = 5000;
        var currentBatch = new List<string>(BatchSize);

        foreach (var file in FileProvider.Files)
        {
            var normalizedKey = file.Key.Replace('\\', '/');
        
            if (normalizedKey.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase) &&
                normalizedKey.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            {
                currentBatch.Add(file.Key); 

                if (currentBatch.Count >= BatchSize)
                {
                    yield return currentBatch;
                    currentBatch = new List<string>(BatchSize);
                    await Task.Yield(); 
                }
            }
        }

        if (currentBatch.Count > 0)
            yield return currentBatch;
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