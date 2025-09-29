using Microsoft.Extensions.Options;
using SimpleDispatch.GeoService.Configuration;
using SimpleDispatch.SharedModels.Geo;
using System.Text.Json;
using System.Web;

namespace SimpleDispatch.GeoService.Services;

/// <summary>
/// Interface for Pelias API client
/// </summary>
public interface IPeliasClient
{
    /// <summary>
    /// Geocode an address to coordinates
    /// </summary>
    /// <param name="address">Address to geocode</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pelias geocoding response</returns>
    Task<PeliasGeocodeResponse?> GeocodeAsync(string address, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverse geocode coordinates to address
    /// </summary>
    /// <param name="latitude">Latitude</param>
    /// <param name="longitude">Longitude</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pelias reverse geocoding response</returns>
    Task<PeliasReverseResponse?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for places
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="latitude">Optional focus latitude</param>
    /// <param name="longitude">Optional focus longitude</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pelias search response</returns>
    Task<PeliasGeocodeResponse?> SearchAsync(string query, double? latitude = null, double? longitude = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get autocomplete suggestions for partial input
    /// </summary>
    /// <param name="text">Partial text for autocomplete</param>
    /// <param name="latitude">Optional focus latitude</param>
    /// <param name="longitude">Optional focus longitude</param>
    /// <param name="layers">Optional layers to filter results (address, venue, locality, etc.)</param>
    /// <param name="sources">Optional sources to filter results (osm, geonames, etc.)</param>
    /// <param name="size">Maximum number of results (default: 10)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pelias autocomplete response</returns>
    Task<PeliasAutocompleteResponse?> AutocompleteAsync(
        string text, 
        double? latitude = null, 
        double? longitude = null, 
        IEnumerable<string>? layers = null,
        IEnumerable<string>? sources = null,
        int size = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// HTTP client for Pelias geocoding API
/// </summary>
public class PeliasClient : IPeliasClient
{
    private readonly HttpClient _httpClient;
    private readonly PeliasOptions _options;
    private readonly ILogger<PeliasClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public PeliasClient(
        HttpClient httpClient,
        IOptions<PeliasOptions> options,
        ILogger<PeliasClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        ConfigureHttpClient();
    }

    /// <inheritdoc/>
    public async Task<PeliasGeocodeResponse?> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Address cannot be null or empty", nameof(address));
        }

        try
        {
            _logger.LogInformation("Geocoding address: {Address}", address);

            var encodedAddress = HttpUtility.UrlEncode(address);
            var url = $"search?text={encodedAddress}";

            if (!string.IsNullOrEmpty(_options.ApiKey))
            {
                url += $"&api_key={_options.ApiKey}";
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<PeliasGeocodeResponse>(jsonContent, _jsonOptions);

            _logger.LogInformation("Successfully geocoded address. Found {Count} results", result?.Features.Count ?? 0);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while geocoding address: {Address}", address);
            throw new InvalidOperationException($"Failed to geocode address: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timed out while geocoding address: {Address}", address);
            throw new TimeoutException("Geocoding request timed out", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Pelias response for address: {Address}", address);
            throw new InvalidOperationException("Invalid response format from Pelias API", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<PeliasReverseResponse?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
        {
            throw new ArgumentException("Invalid coordinates provided");
        }

        try
        {
            _logger.LogInformation("Reverse geocoding coordinates: {Latitude}, {Longitude}", latitude, longitude);

            var url = $"reverse?point.lat={latitude}&point.lon={longitude}";

            if (!string.IsNullOrEmpty(_options.ApiKey))
            {
                url += $"&api_key={_options.ApiKey}";
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<PeliasReverseResponse>(jsonContent, _jsonOptions);

            _logger.LogInformation("Successfully reverse geocoded coordinates. Found {Count} results", result?.Features.Count ?? 0);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while reverse geocoding coordinates: {Latitude}, {Longitude}", latitude, longitude);
            throw new InvalidOperationException($"Failed to reverse geocode coordinates: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timed out while reverse geocoding coordinates: {Latitude}, {Longitude}", latitude, longitude);
            throw new TimeoutException("Reverse geocoding request timed out", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Pelias response for coordinates: {Latitude}, {Longitude}", latitude, longitude);
            throw new InvalidOperationException("Invalid response format from Pelias API", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<PeliasGeocodeResponse?> SearchAsync(string query, double? latitude = null, double? longitude = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be null or empty", nameof(query));
        }

        try
        {
            _logger.LogInformation("Searching places with query: {Query}", query);

            var encodedQuery = HttpUtility.UrlEncode(query);
            var url = $"search?text={encodedQuery}";

            // Add focus point if provided
            if (latitude.HasValue && longitude.HasValue)
            {
                url += $"&focus.point.lat={latitude.Value}&focus.point.lon={longitude.Value}";
            }

            if (!string.IsNullOrEmpty(_options.ApiKey))
            {
                url += $"&api_key={_options.ApiKey}";
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<PeliasGeocodeResponse>(jsonContent, _jsonOptions);

            _logger.LogInformation("Successfully searched places. Found {Count} results", result?.Features.Count ?? 0);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while searching places with query: {Query}", query);
            throw new InvalidOperationException($"Failed to search places: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timed out while searching places with query: {Query}", query);
            throw new TimeoutException("Search request timed out", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Pelias response for query: {Query}", query);
            throw new InvalidOperationException("Invalid response format from Pelias API", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<PeliasAutocompleteResponse?> AutocompleteAsync(
        string text, 
        double? latitude = null, 
        double? longitude = null, 
        IEnumerable<string>? layers = null,
        IEnumerable<string>? sources = null,
        int size = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        }

        if (size <= 0 || size > 100)
        {
            throw new ArgumentException("Size must be between 1 and 100", nameof(size));
        }

        try
        {
            _logger.LogInformation("Getting autocomplete suggestions for text: {Text}", text);
            
            var encodedText = HttpUtility.UrlEncode(text);
            var url = $"autocomplete?text={encodedText}&size={size}";
            
            _logger.LogDebug("Constructed URL: {Url}, Base URL: {BaseUrl}", url, _httpClient.BaseAddress);

            // Add focus point if provided
            if (latitude.HasValue && longitude.HasValue)
            {
                // Validate coordinates
                if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
                {
                    throw new ArgumentException("Invalid coordinates provided");
                }
                url += $"&focus.point.lat={latitude.Value}&focus.point.lon={longitude.Value}";
            }

            // Add layers filter if provided
            if (layers != null)
            {
                var layersList = layers.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                if (layersList.Count > 0)
                {
                    url += $"&layers={string.Join(",", layersList)}";
                }
            }

            // Add sources filter if provided
            if (sources != null)
            {
                var sourcesList = sources.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if (sourcesList.Count > 0)
                {
                    url += $"&sources={string.Join(",", sourcesList)}";
                }
            }

            // Add API key if configured
            if (!string.IsNullOrEmpty(_options.ApiKey))
            {
                url += $"&api_key={_options.ApiKey}";
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<PeliasAutocompleteResponse>(jsonContent, _jsonOptions);

            _logger.LogInformation("Successfully retrieved autocomplete suggestions. Found {Count} results", result?.Features.Count ?? 0);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while getting autocomplete suggestions for text: {Text}", text);
            throw new InvalidOperationException($"Failed to get autocomplete suggestions: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timed out while getting autocomplete suggestions for text: {Text}", text);
            throw new TimeoutException("Autocomplete request timed out", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Pelias autocomplete response for text: {Text}", text);
            throw new InvalidOperationException("Invalid response format from Pelias API", ex);
        }
    }

    private void ConfigureHttpClient()
    {
        // Ensure the base URL ends with a trailing slash for proper relative URL resolution
        var baseUrl = _options.BaseUrl.TrimEnd('/') + "/";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SimpleDispatch.GeoService/1.0");
    }
}