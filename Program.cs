using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// OSRM_URL is set by docker-compose; fall back to localhost when running outside Docker.
var osrmUrl = Environment.GetEnvironmentVariable("OSRM_URL") ?? "http://localhost:5000";
builder.Services.AddHttpClient("osrm", client => client.BaseAddress = new Uri(osrmUrl));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Redirect("/swagger"));

// Proxies a driving route request to OSRM, e.g.
// /route?fromLon=13.388860&fromLat=52.517037&toLon=13.397634&toLat=52.529407
app.MapGet("/route", async (double fromLon, double fromLat, double toLon, double toLat, IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("osrm");
    var coords = string.Format(CultureInfo.InvariantCulture, "{0},{1};{2},{3}", fromLon, fromLat, toLon, toLat);

    using var response = await client.GetAsync($"/route/v1/driving/{coords}?overview=full&geometries=geojson");
    var body = await response.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
})
.WithName("GetRoute")
.WithOpenApi();

app.Run();
