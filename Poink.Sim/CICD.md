# Poink.Sim — CI/CD Commands

Run all commands from the repo root.

## Build (local)

```
dotnet build Poink.Sim/Poink.Sim.csproj -c Debug
```

## Run (local)

```
dotnet run --project Poink.Sim
```

## Build (Docker image)

```
docker build -f Poink.Sim/Dockerfile -t poink-sim .
```

## Compose & deploy (Docker)

```
# Postgres/pgadmin/metabase stack, if not already running:
docker compose up -d

# Build and run Poink.Sim itself:
docker compose -f Poink.Sim/docker-compose.yml up --build -d
```
