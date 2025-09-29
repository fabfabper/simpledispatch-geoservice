using SimpleDispatch.SharedModels;
using SimpleDispatch.GeoService.Configuration;
using SimpleDispatch.GeoService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add configuration
builder.Services.Configure<PeliasOptions>(
    builder.Configuration.GetSection(PeliasOptions.SectionName));

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Add HTTP client for Pelias
builder.Services.AddHttpClient<IPeliasClient, PeliasClient>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
