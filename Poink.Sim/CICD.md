# Poink.Sim — CI/CD Commands

Run all commands from the repo root.

## Build (local)

```
dotnet build Poink.Sim/Poink.Sim.csproj -c Debug
```

## Run (local)

```
# Databases only, then the app on the host:
docker compose up -d database marketdata
dotnet run --project Poink.Sim
```

## Build (Docker image)

```
docker build -f Poink.Sim/Dockerfile -t poink-sim .
```

## Compose & deploy (Docker)

```
# Full stack: app (http://localhost:5000), both databases and pgAdmin (http://localhost:5051):
docker compose up --build -d
```
