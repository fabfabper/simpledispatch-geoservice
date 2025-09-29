namespace SimpleDispatch.GeoService.Configuration;

/// <summary>
/// Configuration settings for Pelias API
/// </summary>
public class PeliasOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Pelias";

    /// <summary>
    /// Base URL for Pelias API
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.pelias.io/v1";

    /// <summary>
    /// API key for Pelias (if required)
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// HTTP client timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}