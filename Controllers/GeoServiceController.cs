using Microsoft.AspNetCore.Mvc;
using SimpleDispatch.SharedModels.Geo;
using SimpleDispatch.GeoService.Services;

namespace SimpleDispatch.GeoService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GeoServiceController : ControllerBase
{
    private readonly ILogger<GeoServiceController> _logger;
    private readonly IPeliasClient _peliasClient;

    public GeoServiceController(ILogger<GeoServiceController> logger, IPeliasClient peliasClient)
    {
        _logger = logger;
        _peliasClient = peliasClient;
    }

    /// <summary>
    /// Get location information by coordinates
    /// </summary>
    /// <param name="latitude">Latitude coordinate</param>
    /// <param name="longitude">Longitude coordinate</param>
    /// <returns>Location information</returns>
    [HttpGet("location")]
    public async Task<ActionResult<LocationInfo>> GetLocationAsync(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting location for coordinates: {Latitude}, {Longitude}", latitude, longitude);

        // Validate coordinates
        if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
        {
            return BadRequest("Invalid coordinates provided");
        }

        try
        {
            var peliasResponse = await _peliasClient.ReverseGeocodeAsync(latitude, longitude, cancellationToken);
            
            if (peliasResponse?.Features?.Count > 0)
            {
                var feature = peliasResponse.Features.First();
                var properties = feature.Properties;

                var locationInfo = new LocationInfo
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    Address = properties.Label ?? "Unknown Address",
                    City = properties.Locality ?? string.Empty,
                    Country = properties.Country ?? string.Empty,
                    PostalCode = properties.Postalcode ?? string.Empty
                };

                return Ok(locationInfo);
            }

            return NotFound("No location information found for the provided coordinates");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting location information for coordinates: {Latitude}, {Longitude}", latitude, longitude);
            return StatusCode(500, "Internal server error while processing location request");
        }
    }

    /// <summary>
    /// Get coordinates by address
    /// </summary>
    /// <param name="address">Address to geocode</param>
    /// <returns>Coordinates for the address</returns>
    [HttpGet("geocode")]
    public async Task<ActionResult<Coordinates>> GeocodeAddressAsync(
        [FromQuery] string address,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Geocoding address: {Address}", address);

        if (string.IsNullOrWhiteSpace(address))
        {
            return BadRequest("Address cannot be empty");
        }

        try
        {
            var peliasResponse = await _peliasClient.GeocodeAsync(address, cancellationToken);
            
            if (peliasResponse?.Features?.Count > 0)
            {
                var feature = peliasResponse.Features.First();
                var geometry = feature.Geometry;
                
                var coordinates = new Coordinates
                {
                    Latitude = geometry.Coordinates[1], // Pelias returns [longitude, latitude]
                    Longitude = geometry.Coordinates[0],
                    Address = feature.Properties.Label ?? address
                };

                return Ok(coordinates);
            }

            return NotFound("No coordinates found for the provided address");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error geocoding address: {Address}", address);
            return StatusCode(500, "Internal server error while processing geocoding request");
        }
    }

    /// <summary>
    /// Calculate distance between two points
    /// </summary>
    /// <param name="request">Distance calculation request</param>
    /// <returns>Distance information</returns>
    [HttpPost("distance")]
    public ActionResult<DistanceInfo> CalculateDistance([FromBody] DistanceRequest request)
    {
        _logger.LogInformation("Calculating distance between two points");

        if (request?.Origin == null || request?.Destination == null)
        {
            return BadRequest("Origin and destination coordinates are required");
        }

        // Simple haversine distance calculation
        var distance = CalculateHaversineDistance(
            request.Origin.Latitude, request.Origin.Longitude,
            request.Destination.Latitude, request.Destination.Longitude);

        var distanceInfo = new DistanceInfo
        {
            Origin = request.Origin,
            Destination = request.Destination,
            DistanceKm = distance,
            DistanceMiles = distance * 0.621371
        };

        return Ok(distanceInfo);
    }

    /// <summary>
    /// Get autocomplete suggestions for partial input
    /// </summary>
    /// <param name="text">Partial text for autocomplete</param>
    /// <param name="focusLat">Optional focus latitude for better results</param>
    /// <param name="focusLon">Optional focus longitude for better results</param>
    /// <param name="layers">Optional comma-separated layers (address,venue,locality)</param>
    /// <param name="sources">Optional comma-separated sources (osm,geonames)</param>
    /// <param name="size">Maximum number of results (1-20, default: 10)</param>
    /// <returns>Autocomplete suggestions</returns>
    [HttpGet("autocomplete")]
    public async Task<ActionResult<List<AutocompleteSuggestion>>> AutocompleteAsync(
        [FromQuery] string text,
        [FromQuery] double? focusLat = null,
        [FromQuery] double? focusLon = null,
        [FromQuery] string? layers = null,
        [FromQuery] string? sources = null,
        [FromQuery] int size = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting autocomplete suggestions for text: {Text}", text);

        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest("Text parameter cannot be empty");
        }

        if (size <= 0 || size > 20)
        {
            return BadRequest("Size must be between 1 and 20");
        }

        try
        {
            // Parse comma-separated layers and sources
            var layersList = string.IsNullOrEmpty(layers) ? null : 
                layers.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(l => l.Trim())
                      .Where(l => !string.IsNullOrEmpty(l));

            var sourcesList = string.IsNullOrEmpty(sources) ? null :
                sources.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim())
                       .Where(s => !string.IsNullOrEmpty(s));

            var peliasResponse = await _peliasClient.AutocompleteAsync(
                text, focusLat, focusLon, layersList, sourcesList, size, cancellationToken);
            
            if (peliasResponse?.Features?.Count > 0)
            {
                var suggestions = peliasResponse.Features.Select(feature =>
                {
                    var properties = feature.Properties;
                    var geometry = feature.Geometry;
                    
                    return new AutocompleteSuggestion
                    {
                        Label = properties.Label ?? "Unknown Location",
                        Latitude = geometry.Coordinates[1], // Pelias returns [longitude, latitude]
                        Longitude = geometry.Coordinates[0],
                        Layer = properties.Layer ?? string.Empty,
                        Source = properties.Source ?? string.Empty,
                        Confidence = properties.Confidence,
                        City = properties.Locality ?? string.Empty,
                        Country = properties.Country ?? string.Empty,
                        Region = properties.Region ?? string.Empty
                    };
                }).ToList();

                return Ok(suggestions);
            }

            return Ok(new List<AutocompleteSuggestion>()); // Return empty list instead of 404 for autocomplete
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting autocomplete suggestions for text: {Text}", text);
            return StatusCode(500, "Internal server error while processing autocomplete request");
        }
    }

    /// <summary>
    /// Search for places using Pelias
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="focusLat">Optional focus latitude for better results</param>
    /// <param name="focusLon">Optional focus longitude for better results</param>
    /// <returns>Search results</returns>
    [HttpGet("search")]
    public async Task<ActionResult<List<LocationInfo>>> SearchPlacesAsync(
        [FromQuery] string query,
        [FromQuery] double? focusLat = null,
        [FromQuery] double? focusLon = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching places with query: {Query}", query);

        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Search query cannot be empty");
        }

        try
        {
            var peliasResponse = await _peliasClient.SearchAsync(query, focusLat, focusLon, cancellationToken);
            
            if (peliasResponse?.Features?.Count > 0)
            {
                var locations = peliasResponse.Features.Select(feature =>
                {
                    var properties = feature.Properties;
                    var geometry = feature.Geometry;
                    
                    return new LocationInfo
                    {
                        Latitude = geometry.Coordinates[1], // Pelias returns [longitude, latitude]
                        Longitude = geometry.Coordinates[0],
                        Address = properties.Label ?? "Unknown Address",
                        City = properties.Locality ?? string.Empty,
                        Country = properties.Country ?? string.Empty,
                        PostalCode = properties.Postalcode ?? string.Empty
                    };
                }).ToList();

                return Ok(locations);
            }

            return NotFound("No places found for the provided search query");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching places with query: {Query}", query);
            return StatusCode(500, "Internal server error while processing search request");
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    /// <returns>Service health status</returns>
    [HttpGet("health")]
    public ActionResult<object> GetHealth()
    {
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }

    private static double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        lat1 = DegreesToRadians(lat1);
        lat2 = DegreesToRadians(lat2);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}