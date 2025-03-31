using Newtonsoft.Json;

namespace Helios.Classes.Endpoints.Lightswitch;

public class LauncherInfoDTO
{
    [JsonProperty("appName")]
    public string AppName {  get; set; }
    [JsonProperty("catalogItemId")]
    public string CatalogItemId { get; set; }
    [JsonProperty("namespace")]
    public string Namespace { get; set; }
}