# Poink.CTrader

A standalone .NET class library (net10.0) for talking to the [cTrader Open API](https://help.ctrader.com/open-api/) over TCP/protobuf. Vendored from Spotware's [`OpenAPI.Net`](https://github.com/spotware/OpenAPI.Net) plus custom code on top. It has no EF/Postgres or UI-framework dependency, so any project can reference it.

See [TODO.md](TODO.md) for the current backlog.

## Two layers of code

**Vendored (namespace `OpenAPI.Net.*`)** - `Auth/`, `Exceptions/`, `Helpers/`, `Messages/`, `OpenClient.cs`. Copied from Spotware's reference implementation, retargeted to net10.0, package versions bumped. There is no `.proto` source upstream - only the already protoc-generated C# message classes - so there is no proto-compilation step to maintain. Treat these files as third-party: don't "clean up" their style, and if Spotware ships new message types, they'd need to be re-copied from upstream (no automated sync exists).

**Ours (namespace `Poink.CTrader`)** - everything else. This is the actual integration surface other projects should use.

## The moving pieces (`Poink.CTrader`)

| Type | Purpose |
|---|---|
| `CTraderApiOptions` | Static app credentials (`ClientId`, `ClientSecret`, `RedirectUri`, `UseLiveEnvironment`), bound via `IOptions<T>` from config section `"CTrader"`. |
| `ICTraderTokenStore` | Abstraction over persisting a per-user OAuth `Token` (access/refresh). Deliberately has no EF/Postgres dependency here - see "Where the pieces live" below. |
| `ICTraderAuthUiService` | Optional abstraction over capturing the OAuth authorization code in a native login UI. Poink.Sim doesn't use it; it handles the redirect server-side instead. |
| `ICTraderClient` / `CTraderClient` | The actual protocol wrapper around the vendored `OpenClient`. One instance = one connection attempt (construct a new one to retry; no reconnect/backoff). Exposes `Messages` (`IObservable<IMessage>`, backed by a `Subject` so it's observable even before connecting) plus typed request/response methods: `ConnectAsync` (socket + app-auth handshake), `GetAccountsAsync`, `AuthorizeAccountAsync`, `GetSymbolsAsync`, `SubscribeSpotsAsync`/`UnsubscribeSpotsAsync`, `GetTrendbarsAsync`, `GetDealsAsync` and more. |
| `CTraderApiException` | Thrown when cTrader responds with a `ProtoOAErrorRes` instead of the expected message; carries `ErrorCode`/`Description`. |
| `ServiceCollectionExtensions.AddCTraderOpenApi` | Binds `CTraderApiOptions`, registers an `App` (vendored auth type) built from those options, and registers `ICTraderClient`. Callers must separately register their own `ICTraderTokenStore`. |

### How `CTraderClient` request/response methods work

Every typed method (`GetAccountsAsync`, `AuthorizeAccountAsync`, etc.) goes through a private generic `SendAndWaitAsync<TRes>(request, cancellationToken)`: it subscribes to `Messages` for either the expected `TRes` type or a `ProtoOAErrorRes` (-> throws `CTraderApiException`), sends the request, and races that against a 15s timeout. **Adding a new cTrader call is almost always just one more method following this exact pattern** - look at any existing one in `CTraderClient.cs` as a template.

## Where the pieces live (Poink.Sim)

The concrete implementations that plug into this library's interfaces live in `Poink.Sim`:

- `Services/Registration/ServiceCollectionExtensions.cs` - calls `AddCTraderOpenApi` and registers `PostgresCTraderTokenStore`.
- `Data/Entities/CTraderAccountToken.cs` + `Services/CTrader/PostgresCTraderTokenStore.cs` - Postgres-backed `ICTraderTokenStore`.
- `Services/CTrader/CTraderAuthEndpoints.cs` - maps the OAuth redirect at the path of the configured `RedirectUri` (default `http://localhost:5001/ctrader/callback`), exchanges the code for a token server-side, then returns to `/trade`.
- `Services/CTrader/CTraderSessionService.cs` (+ `CTraderSessionRestoreService`) - owns the connection, account selection, spot subscriptions and restoring a saved session on startup.
- `Components/Pages/Trade/` - the UI: login, account menu, live prices, history and trading.

**To use `Poink.CTrader` from a new project:** add a `ProjectReference`, call `AddCTraderOpenApi(configuration)`, and register your own `ICTraderTokenStore`.

## Secrets

`CTraderApiOptions` binds from config section `"CTrader"` (`ClientId`, `ClientSecret`, `RedirectUri`, `UseLiveEnvironment`). **Never put real values in a tracked `appsettings*.json`.** Supply them via:
- `Poink.Sim/appsettings.Secrets.json` (gitignored, loaded by `Program.cs` on top of `appsettings.json`).
- Environment variables `CTrader__ClientId`, `CTrader__ClientSecret`, `CTrader__RedirectUri` (double underscore = nested config key).

The redirect URI must match one registered for your application in the cTrader Open API portal. When `ClientId`/`ClientSecret`/`RedirectUri` are empty, the Trade page shows "cTrader is not configured" rather than failing silently.

## cTrader-specific gotchas worth knowing before extending this

- **Prices are scaled integers**: spot quotes (`ProtoOASpotEvent.Bid`/`.Ask`) and trendbar prices are all `raw / 100_000.0` to get the real decimal price. This came from the generated code's own doc comments, not guesswork.
- **Trendbars are delta-encoded**: `ProtoOATrendbar.Low` is absolute; `DeltaOpen`/`DeltaHigh`/`DeltaClose` are offsets *added* to `Low`. `UtcTimestampInMinutes` is minutes-since-epoch, not milliseconds.
- **Sequencing matters**: you must await `ProtoOAApplicationAuthRes` before sending anything else (sending requests back-to-back races the handshake and gets rejected). `CTraderClient.ConnectAsync` already handles this; don't bypass it by sending directly on the underlying `OpenClient`.
- **Account authorization is required before account-scoped calls** (`GetSymbolsAsync`, `SubscribeSpotsAsync`, `GetTrendbarsAsync`, etc.) - call `AuthorizeAccountAsync` first. `GetAccountsAsync` alone only lists accounts, it doesn't authorize any of them.
- **cTrader enforces its own max time-range-per-trendbar-period limit** server-side (exact values undocumented) - an invalid combination just surfaces as a normal `CTraderApiException`, there's no client-side pre-validation.
- **One `CTraderClient` = one connection, no auto-reconnect.** Dispose and recreate it to reconnect.
- **Live trading over Open API may be blocked by the broker** (`CHANNEL_IS_BLOCKED`). Poink.Sim routes live orders through the `cBots/OrderBot` localhost bridge instead.

## Extending this further

- New API call -> add a method to `ICTraderClient`/`CTraderClient` following the `SendAndWaitAsync<TRes>` pattern.
- Check [TODO.md](TODO.md) before starting anything - it lists known gaps (no refresh-token flow, no reconnect, etc.) so you don't rediscover them the hard way.
