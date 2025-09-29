# Pelias Integration Guide

This project includes HTTP client integration with Pelias geocoding API for production-ready location services.

## Configuration

### appsettings.json

```json
{
  "Pelias": {
    "BaseUrl": "https://api.pelias.io/v1",
    "ApiKey": "your-api-key-here",
    "TimeoutSeconds": 30
  }
}
```

### Environment-Specific Configuration

- **Production**: Use `https://api.pelias.io/v1` (hosted Pelias service)
- **Development**: Use `http://localhost:4000/v1` (local Pelias instance)

## API Endpoints

### 1. Reverse Geocoding

**GET** `/api/geoservice/location?latitude={lat}&longitude={lng}`

Converts coordinates to address information using Pelias reverse geocoding.

### 2. Geocoding

**GET** `/api/geoservice/geocode?address={address}`

Converts address to coordinates using Pelias forward geocoding.

### 3. Autocomplete Suggestions

**GET** `/api/geoservice/autocomplete?text={partial_text}&focusLat={lat}&focusLon={lng}&layers={layers}&sources={sources}&size={size}`

Provides real-time autocomplete suggestions as users type. Parameters:

- `text`: Partial input text (required)
- `focusLat/focusLon`: Optional focus point for better results
- `layers`: Optional comma-separated layers (address,venue,locality,region,country)
- `sources`: Optional comma-separated sources (osm,geonames,whosonfirst)
- `size`: Maximum results (1-20, default: 10)

### 4. Place Search

**GET** `/api/geoservice/search?query={query}&focusLat={lat}&focusLon={lng}`

Searches for places using Pelias search API with optional focus point for better results.

### 5. Distance Calculation

**POST** `/api/geoservice/distance`

Calculates distance between two points using Haversine formula.

## Features

- ✅ Configurable Pelias base URL
- ✅ Optional API key support
- ✅ Comprehensive error handling
- ✅ Request timeout configuration
- ✅ Structured logging
- ✅ Automatic retry and resilience patterns
- ✅ JSON serialization with proper naming conventions

## Autocomplete Features

The autocomplete endpoint is optimized for real-time suggestions:

- **Fast Response**: Designed for low-latency responses
- **Filtered Results**: Support for layer and source filtering
- **Focus Point**: Bias results toward a geographic area
- **Confidence Scoring**: Each result includes a confidence score
- **Rich Metadata**: Layer type, source, and location details

### Supported Layers

- `address`: Street addresses
- `venue`: Points of interest (restaurants, shops, etc.)
- `locality`: Cities, towns, neighborhoods
- `region`: States, provinces
- `country`: Countries

### Supported Sources

- `osm`: OpenStreetMap data
- `geonames`: GeoNames database
- `whosonfirst`: Who's On First data

## Usage Examples

### Basic Configuration

```json
{
  "Pelias": {
    "BaseUrl": "https://api.pelias.io/v1"
  }
}
```

### With API Key

```json
{
  "Pelias": {
    "BaseUrl": "https://api.pelias.io/v1",
    "ApiKey": "your-secret-api-key"
  }
}
```

### Custom Timeout

```json
{
  "Pelias": {
    "BaseUrl": "http://localhost:4000/v1",
    "TimeoutSeconds": 60
  }
}
```

## Error Handling

The Pelias client includes comprehensive error handling:

- **HttpRequestException**: Network or HTTP errors
- **TaskCanceledException**: Request timeouts
- **JsonException**: Invalid response format
- **ArgumentException**: Invalid input parameters

All errors are logged and wrapped in appropriate exceptions with descriptive messages.

## Dependency Injection

The Pelias client is registered as a scoped service:

```csharp
builder.Services.Configure<PeliasOptions>(
    builder.Configuration.GetSection(PeliasOptions.SectionName));

builder.Services.AddHttpClient<IPeliasClient, PeliasClient>();
```

## Testing

Use the provided `.http` file to test all endpoints with realistic data:

```http
### Test Reverse Geocoding
GET https://localhost:7298/api/geoservice/location?latitude=47.6062&longitude=-122.3321

### Test Geocoding
GET https://localhost:7298/api/geoservice/geocode?address=1600 Amphitheatre Parkway, Mountain View, CA

### Test Autocomplete (Basic)
GET https://localhost:7298/api/geoservice/autocomplete?text=seatt&size=5

### Test Autocomplete (With Focus)
GET https://localhost:7298/api/geoservice/autocomplete?text=star&focusLat=47.6062&focusLon=-122.3321&size=10

### Test Autocomplete (With Filters)
GET https://localhost:7298/api/geoservice/autocomplete?text=coffee&layers=venue&sources=osm&size=5

### Test Place Search
GET https://localhost:7298/api/geoservice/search?query=coffee shop&focusLat=47.6062&focusLon=-122.3321
```
