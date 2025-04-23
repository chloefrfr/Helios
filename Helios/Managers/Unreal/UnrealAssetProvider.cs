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
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _isInitialized;
    private bool _isDisposed;
    private static readonly HttpClient _httpClient = new();
    private const string CosmeticsPath = "FortniteGame/Content/Athena/Items/Cosmetics";
    
    public DefaultFileProvider FileProvider { get; private set; }

    public UnrealAssetProvider()
    {
        var dataPath = Path.Combine(Directory.GetCurrentDirectory(), ".data");
        Directory.CreateDirectory(dataPath); // No-op if exists
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return;

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized) return;

            Logger.Info($"Initializing provider with game directory: {Constants.config.GameDirectory}");
            ValidateGameDirectory(Constants.config.GameDirectory);

            var version = FVersionConverter.Convert(Constants.config.CurrentVersion);
            FileProvider = new DefaultFileProvider(
                Constants.config.GameDirectory,
                SearchOption.TopDirectoryOnly,
                true,
                new VersionContainer(EGame.GAME_UE4_LATEST));

            FileProvider.Initialize();

            var aesKey = FAesProvider.GetAesKey(version) 
                ?? throw new InvalidOperationException($"Failed to get AES key for version {Constants.config.CurrentVersion}");
            
            await FileProvider.SubmitKeysAsync([new(new FGuid(), new FAesKey(aesKey.Key))], cancellationToken);

            var dataPath = Path.Combine(Directory.GetCurrentDirectory(), ".data");
            await InitializeOodleAsync(dataPath);
            
            FileProvider.MappingsContainer = await GetMappingsAsync(dataPath);
            
            PrecomputeCosmeticPaths();
            
            Logger.Info($"Provider initialized with version {Constants.config.CurrentVersion}");
            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task InitializeOodleAsync(string dataPath)
    {
        var oodlePath = Path.Combine(dataPath, OodleHelper.OODLE_DLL_NAME);
        if (!File.Exists(oodlePath))
        {
            await OodleHelper.DownloadOodleDllAsync(oodlePath);
        }
        OodleHelper.Initialize(oodlePath);
    }

    private async Task<FileUsmapTypeMappingsProvider> GetMappingsAsync(string dataPath)
    {
        var mappingsResponse = await _httpClient.GetStringAsync("https://fortnitecentral.genxgames.gg/api/v1/mappings");
        var mappingsData = JsonConvert.DeserializeObject<List<MappingsResponse>>(mappingsResponse)[0];
        
        var path = Path.Combine(dataPath, mappingsData.FileName);
        if (!File.Exists(path))
        {
            var bytes = await _httpClient.GetByteArrayAsync(mappingsData.Url);
            await File.WriteAllBytesAsync(path, bytes);
        }

        return new FileUsmapTypeMappingsProvider(path);
    }

    private void PrecomputeCosmeticPaths()
    {
        var sw = Stopwatch.StartNew();
        var cosmetics = FileProvider.Files
            .AsParallel()
            .Where(file => file.Key.StartsWith(CosmeticsPath, StringComparison.OrdinalIgnoreCase) 
                           && file.Key.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            .Select(file => file.Key)
            .ToList();

        HeliosFastCache.Set(CosmeticsPath, cosmetics);
        Logger.Info($"Precomputed {cosmetics.Count} cosmetic paths in {sw.ElapsedMilliseconds}ms");
    }

    public Task<List<string>> LoadAllCosmeticsAsync(CancellationToken cancellationToken = default)
    {
        return HeliosFastCache.TryGet<List<string>>(CosmeticsPath, out var cached)
            ? Task.FromResult(cached)
            : Task.FromResult(new List<string>());
    }

    private void ValidateGameDirectory(string gameDirectory)
    {
        if (!Directory.Exists(gameDirectory))
            throw new DirectoryNotFoundException($"Game directory not found: {gameDirectory}");

        try
        {
            Directory.GetFileSystemEntries(gameDirectory);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"Access denied to game directory: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        
        FileProvider?.Dispose();
        _initializationLock?.Dispose();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}