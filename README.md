# Poink.Sim

A Blazor Server workbench for researching algorithmic trading strategies on historical tick data:
replay ticks, train reinforcement-learning "brains", study signals, and route trades to cTrader.

> **Disclaimer:** this is a personal research project, not financial advice. Trading leveraged
> products carries real risk of loss. Use the live-trading parts at your own risk.

## Features

- **Simulation** – tick-by-tick replay with play/pause/step/speed, a pluggable Brain deciding and a
  Trader executing under leveraged CFD mechanics.
- **Training** – headless reinforcement learning (TorchSharp) over historical episodes.
- **Profiler, Signal Study, Analyze** – volume/time profiles, chart annotations and signal research.
- **Trade** – live tick recording, indicators and a quick-trade console against cTrader.
- **Emulator** – run bot groups against a simulated brokerage.
- **Data** – import and inspect tick coverage per symbol and day.
- **Reflection** – in-app views of the codebase's own structure and call graph.

## Repository layout

| Project | Purpose |
|---|---|
| `Poink.Sim` | The Blazor Server app. |
| `Poink.Sim.Tests` | xUnit tests. |
| `Poink.CTrader` | cTrader Open API client (TCP/protobuf). |
| `Poink.ZTicks` / `Poink.Sim.Tools.ZTicksImport` | Reader and CLI importer for cTrader `.zticks` backtesting cache files. |
| `Poink.Analyzers` | Roslyn analyzers enforcing the project's code style. |
| `cBots/` | cTrader cBots: `Poink.Trader` (tick recorder) and `OrderBot` (localhost order bridge). |
| `Poink.Obsidian` | Generated Obsidian vault mapping the codebase. |

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) for the TimescaleDB/Postgres databases
- Optional: [cTrader](https://ctrader.com/) and an Open API application for live data and trading

## Getting started

```sh
# Start the app database (port 5434) and market-data store (port 5433)
docker compose up -d database marketdata

# Run the app (migrations apply on startup)
dotnet run --project Poink.Sim
```

Open <http://localhost:5001>.

To load history, import cTrader backtesting cache files:

```sh
dotnet run --project Poink.Sim.Tools.ZTicksImport -- --cache <path-to-t1-folder> --symbol <name> --env live
```

On Windows the cache lives under `%APPDATA%\Spotware\Cache\<broker>\BacktestingCache\V1\<account>\<symbol>\t1`.

## Configuration

`Poink.Sim/appsettings.json` holds local defaults only. Put credentials in a gitignored
`Poink.Sim/appsettings.Secrets.json`, which overrides it:

```json
{
  "CTrader": { "ClientId": "...", "ClientSecret": "..." },
  "OrderBot": { "CommandToken": "..." }
}
```

## Tests

```sh
dotnet test Poink.Sim.Tests
```

## License

[MIT](LICENSE)
