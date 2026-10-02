# OSRM + .NET 8 Routing API

A .NET 8 Web API that calculates driving routes with [OSRM](https://project-osrm.org/) (Open Source Routing Machine). Both services run in Docker:

| Service        | Container             | Port | Purpose                                   |
| -------------- | --------------------- | ---- | ----------------------------------------- |
| `osrm-backend` | `osrm_routing_server` | 5000 | Routing engine, serving the Berlin map    |
| `dotnet-api`   | `dotnet_web_api`      | 8080 | C# API that forwards route requests to OSRM |

The API reaches OSRM through the `OSRM_URL` environment variable. Docker Compose sets it to `http://osrm-backend:5000`. Outside Docker it defaults to `http://localhost:5000`.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) with Docker Compose v2
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). You only need this to build or run the API outside Docker.
- About 2 GB of free disk space and RAM for preparing the map

## Setup

### 1. Prepare the map data (one-time)

OSRM can't read raw OpenStreetMap data. It has to be preprocessed first. The commands below download the Berlin extract from Geofabrik and prepare it for the MLD algorithm, which is the one the server uses. This takes a few minutes.

**PowerShell:**

```powershell
mkdir osrm-data
Invoke-WebRequest https://download.geofabrik.de/europe/germany/berlin-latest.osm.pbf -OutFile osrm-data\berlin-latest.osm.pbf

docker run --rm -v "${PWD}\osrm-data:/data" osrm/osrm-backend osrm-extract -p /opt/car.lua /data/berlin-latest.osm.pbf
docker run --rm -v "${PWD}\osrm-data:/data" osrm/osrm-backend osrm-partition /data/berlin-latest.osrm
docker run --rm -v "${PWD}\osrm-data:/data" osrm/osrm-backend osrm-customize /data/berlin-latest.osrm
```

**Bash (Linux/macOS):**

```bash
mkdir -p osrm-data
curl -L -o osrm-data/berlin-latest.osm.pbf https://download.geofabrik.de/europe/germany/berlin-latest.osm.pbf

docker run --rm -v "$PWD/osrm-data:/data" osrm/osrm-backend osrm-extract -p /opt/car.lua /data/berlin-latest.osm.pbf
docker run --rm -v "$PWD/osrm-data:/data" osrm/osrm-backend osrm-partition /data/berlin-latest.osrm
docker run --rm -v "$PWD/osrm-data:/data" osrm/osrm-backend osrm-customize /data/berlin-latest.osrm
```

When this finishes, `osrm-data/` contains `berlin-latest.osrm.*` files.

> **Using a different region:** download another `.osm.pbf` from [Geofabrik](https://download.geofabrik.de/) and run the same three commands on it. Then update the file name in the `command:` line of `docker-compose.yml`.

### 2. Start the services

```bash
docker compose up -d
```

The first start takes about 30 seconds while the API container restores NuGet packages. To follow the progress:

```bash
docker compose logs -f dotnet-api
```

The API runs under `dotnet watch`, so it reloads automatically when you save changes to the C# code.

### 3. Stop the services

```bash
docker compose down
```

## Testing

### Swagger UI

Open http://localhost:8080/swagger in a browser. Expand `GET /route`, click **Try it out**, enter coordinates and run the request.

### API endpoint

```
GET /route?fromLon={lon}&fromLat={lat}&toLon={lon}&toLat={lat}
```

Coordinates use decimal degrees. Example route, from the Brandenburg Gate area to Rosenthaler Platz:

```bash
curl "http://localhost:8080/route?fromLon=13.388860&fromLat=52.517037&toLon=13.397634&toLat=52.529407"
```

A successful response looks like this (shortened):

```json
{
  "code": "Ok",
  "routes": [
    {
      "geometry": { "type": "LineString", "coordinates": [[13.38882, 52.517034], ...] },
      "distance": 1885.4,
      "duration": 259.7,
      ...
    }
  ],
  "waypoints": [...]
}
```

- `distance` is in meters and `duration` is in seconds.
- `geometry` is GeoJSON, so you can draw it directly on a map (for example with Leaflet or geojson.io).

**PowerShell alternative:**

```powershell
Invoke-RestMethod "http://localhost:8080/route?fromLon=13.388860&fromLat=52.517037&toLon=13.397634&toLat=52.529407"
```

You can also send the request from [OsrmApi.http](OsrmApi.http) in VS Code (REST Client extension) or Visual Studio.

### Testing OSRM directly

To check the routing engine without going through the API:

```bash
curl "http://localhost:5000/route/v1/driving/13.388860,52.517037;13.397634,52.529407?overview=false"
```

`"code":"Ok"` means OSRM is running and the map loaded correctly.

## Running the API without Docker

You still need OSRM running on port 5000. Start it on its own, then run the API on your machine:

```bash
docker compose up -d osrm-backend
dotnet run
```

The API listens on the URL shown in the console (see `Properties/launchSettings.json`).

## Troubleshooting

| Problem | Cause / fix |
| ------- | ----------- |
| `osrm_routing_server` keeps restarting | The map data is missing or incomplete. Check that `osrm-data/berlin-latest.osrm.*` files exist and redo step 1. |
| `"code":"NoSegment"` or `"NoRoute"` | The coordinates are outside the loaded map. Only Berlin is included by default. |
| Wrong location returned | Parameters are in **longitude, latitude** order. Swapping them is the most common mistake. |
| API doesn't respond right after startup | The container is still restoring packages. Watch `docker compose logs -f dotnet-api`. |
| Build errors inside the container after building on the host | Delete the local `bin/` and `obj/` folders. Builds made on the host can conflict with the container's Linux build. |

## Project structure

```
.
├── docker-compose.yml      # OSRM + API services
├── Program.cs              # API setup and /route endpoint
├── OsrmApi.csproj          # .NET 8 project file
├── OsrmApi.http            # Sample HTTP requests
├── appsettings*.json       # ASP.NET Core configuration
└── osrm-data/              # Map data (downloaded and generated; not committed)
```
