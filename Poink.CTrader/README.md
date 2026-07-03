# Poink.CTrader

A standalone .NET class library (net10.0) for talking to the [cTrader Open API](https://help.ctrader.com/open-api/) over TCP/protobuf. Vendored from Spotware's `OpenAPI.Net` (via [ETJoubert/OpenAPI.Net](https://github.com/ETJoubert/OpenAPI.Net) fork) plus custom code on top. Designed to be referenced from any project in this solution (or elsewhere) without pulling in EF/Postgres or any particular UI framework.

See [LOGBOOK.md](LOGBOOK.md) for the detailed, chronological history of every decision and bug fix, and [TODO.md](TODO.md) for the current backlog. This file is the "start here" summary.

## Two layers of code

**Vendored (namespace `OpenAPI.Net.*`)** - `Auth/`, `Exceptions/`, `Helpers/`, `Messages/`, `OpenClient.cs`. Copied from Spotware's reference implementation, retargeted to net10.0, package versions bumped. There is no `.proto` source anywhere in that fork - only the already protoc-generated C# message classes - so there is no proto-compilation step to maintain. Treat these files as third-party: don't "clean up" their style, and if Spotware ships new message types, they'd need to be re-copied from upstream (no automated sync exists).

**Ours (namespace `Poink.CTrader`)** - everything else. This is the actual integration surface other projects should use.

## The moving pieces (`Poink.CTrader`)

| Type | Purpose |
|---|---|
| `CTraderApiOptions` | Static app credentials (`ClientId`, `ClientSecret`, `RedirectUri`, `UseLiveEnvironment`), bound via `IOptions<T>` from config section `"CTrader"`. |
| `ICTraderTokenStore` | Abstraction over persisting a per-user OAuth `Token` (access/refresh). Deliberately has no EF/Postgres dependency here - see "Where the pieces actually live" below. |
| `ICTraderAuthUiService` | Abstraction over capturing the OAuth authorization code from the login flow. Platform-specific implementations live outside this project (see below). |
| `ICTraderClient` / `CTraderClient` | The actual protocol wrapper around the vendored `OpenClient`. One instance = one connection attempt (construct a new one to retry; no reconnect/backoff). Exposes `Messages` (`IObservable<IMessage>`, backed by a `Subject` so it's observable even before connecting) plus typed request/response methods: `ConnectAsync` (socket + app-auth handshake), `GetAccountsAsync`, `AuthorizeAccountAsync`, `GetSymbolsAsync`, `SubscribeSpotsAsync`/`UnsubscribeSpotsAsync`, `GetTrendbarsAsync`. |
| `CTraderApiException` | Thrown when cTrader responds with a `ProtoOAErrorRes` instead of the expected message; carries `ErrorCode`/`Description`. |
| `ServiceCollectionExtensions.AddCTraderOpenApi` | Binds `CTraderApiOptions`, registers an `App` (vendored auth type) built from those options, and registers `ICTraderClient`. Callers must separately register their own `ICTraderTokenStore` and `ICTraderAuthUiService`. |

### How `CTraderClient` request/response methods work

Every typed method (`GetAccountsAsync`, `AuthorizeAccountAsync`, etc.) goes through a private generic `SendAndWaitAsync<TRes>(request, cancellationToken)`: it subscribes to `Messages` for either the expected `TRes` type or a `ProtoOAErrorRes` (-> throws `CTraderApiException`), sends the request, and races that against a 15s timeout. **Adding a new cTrader call is almost always just one more method following this exact pattern** - look at any existing one in `CTraderClient.cs` as a template.

## Where the pieces actually live (cross-project wiring)

`Poink.CTrader` is intentionally dependency-light (EF-free, UI-framework-free) so it stays reusable. The concrete implementations that plug into its interfaces live in the consuming projects:

- **`Poink.Shared`** (references `Poink.CTrader` via `ProjectReference`):
  - `Data/Entities/CTraderAccountToken.cs` + `PoinkContext.CTraderAccountTokens` - the Postgres entity backing token storage.
  - `Services/CTrader/PostgresCTraderTokenStore.cs` - `ICTraderTokenStore` implementation using `PoinkContext`.
  - `Services/Registration/CTraderOptionsInstaller.cs` - an `IConfiguredServiceInstaller`, auto-discovered by `Poink.Shared.Core.ServiceRegistrar`'s reflection scan (which only scans the `Poink.Shared` assembly - this is *why* the store implementation and DI wiring live here, not in `Poink.CTrader`). Calls `AddCTraderOpenApi` + registers `PostgresCTraderTokenStore`.
  - `Components/Pages/CTrader/CTraderPage.razor(.cs/.css)` - the actual UI: connect, pick an account (dropdown in a sticky top bar), search/select a symbol (side panel), watch its live bid/ask, load and view historical bars (table or candlestick graph).
  - `Components/Pages/CTrader/CandlestickChart.razor(.cs/.css)` - a small wrapper around a hand-rolled `<canvas>` chart (see below).
  - `Core/Features/BuiltInFeatures.cs` - the `CTrader` feature entry (nav route `/ctrader`); gate it in an app's `appsettings.json` `Features:Enabled` list.

- **`Poink` (MAUI)**:
  - `Pages/CTraderLoginPage.xaml(.cs)` - native `WebView`-hosted OAuth login page. Intercepts the browser's *attempted* navigation to the redirect URI and cancels it before it's ever requested - **the redirect URI never needs to be a real, reachable endpoint**, it just has to match what's registered with Spotware.
  - `Services/MauiCTraderAuthUiService.cs` - `ICTraderAuthUiService` implementation; pushes `CTraderLoginPage` modally via `Application.Current.Windows[0].Page.Navigation`.
  - `MauiProgram.cs` - registers `ICTraderAuthUiService` directly (not through `ServiceRegistrar`, same pattern as the existing `ITokenStore`/`LocalStorageTokenStore`), and calls `builder.Configuration.AddEnvironmentVariables()` so secrets can be supplied without touching a tracked file.

**If you want to use `Poink.CTrader` from a new project** (e.g. a background worker, `Poink.Web`, or an entirely different app): add a `ProjectReference`, call `AddCTraderOpenApi(configuration)`, and register your own `ICTraderTokenStore` + `ICTraderAuthUiService` for that context (or reuse `PostgresCTraderTokenStore` if it's also EF/Postgres-backed via `Poink.Shared`).

## Secrets

`CTraderApiOptions` binds from config section `"CTrader"` (`ClientId`, `ClientSecret`, `RedirectUri`, `UseLiveEnvironment`). **Never put real values in a tracked `appsettings*.json`** - all three of `ClientId`/`ClientSecret`/`RedirectUri` are treated as sensitive here (the redirect URI in this project embeds a real IP). Supply real values via:
- MAUI (`Poink`): environment variables `CTrader__ClientId`, `CTrader__ClientSecret`, `CTrader__RedirectUri` (double underscore = nested config key). Set via `setx` (persistent, requires fully restarting Visual Studio/the app afterward) or a terminal session before running.
- `Poink.Web`: `dotnet user-secrets set CTrader:ClientSecret "..." --project Poink.Web` (has a `UserSecretsId`).

`CTraderPage.ConnectAsync` checks for empty `ClientId`/`Secret`/`RedirectUri` up front and shows a clear "not configured" message rather than silently failing - if you see that, the secrets aren't reaching the process.

## cTrader-specific gotchas worth knowing before extending this

- **Prices are scaled integers**: spot quotes (`ProtoOASpotEvent.Bid`/`.Ask`) and trendbar prices are all `raw / 100_000.0` to get the real decimal price. This came from the generated code's own doc comments, not guesswork.
- **Trendbars are delta-encoded**: `ProtoOATrendbar.Low` is absolute; `DeltaOpen`/`DeltaHigh`/`DeltaClose` are offsets *added* to `Low`. `UtcTimestampInMinutes` is minutes-since-epoch, not milliseconds.
- **Sequencing matters**: you must await `ProtoOAApplicationAuthRes` before sending anything else (a real bug we hit - sending requests back-to-back raced the handshake and got rejected). `CTraderClient.ConnectAsync` already handles this; don't bypass it by sending directly on the underlying `OpenClient`.
- **Account authorization is required before account-scoped calls** (`GetSymbolsAsync`, `SubscribeSpotsAsync`, `GetTrendbarsAsync`, etc.) - call `AuthorizeAccountAsync` first. `GetAccountsAsync` alone only lists accounts, it doesn't authorize any of them.
- **cTrader enforces its own max time-range-per-trendbar-period limit** server-side (undocumented exact values as far as this project has determined) - an invalid combination just surfaces as a normal `CTraderApiException`, there's no client-side pre-validation.
- **One `CTraderClient` = one connection, no auto-reconnect.** `CTraderPage` holds a single instance across multiple UI interactions (connect once, then select accounts/symbols/history against the same connection) and disposes/recreates it on each fresh "Connect" click.

## The custom candlestick chart

No third-party JS charting library is vendored (an attempt to fetch TradingView's `lightweight-charts` from a CDN was blocked by this environment's safety classifier as an unapproved external-code fetch; asked, the user chose to build custom instead). `Poink.Shared/ts/candlestickChart.ts` is a dependency-free `<canvas>` 2D renderer, compiled via the project's existing `tsc` MSBuild step (same pattern as `poinkLogoGrid.ts`) into `wwwroot/js/candlestickChart.js`. It reads this app's own `--color-success`/`--color-error` CSS variables so it matches the theme automatically. It already exports an `update(canvas, bar)` function (append/replace the last bar) for future real-time streaming, but nothing currently calls it - the live spot-price subscription (`CTraderPage`'s "WATCH PRICE") and the chart are still two independent features (see TODO).

## Extending this further

- New API call -> add a method to `ICTraderClient`/`CTraderClient` following the `SendAndWaitAsync<TRes>` pattern.
- New UI feature -> lives in `Poink.Shared/Components/Pages/CTrader/` (feature-specific components, not `Components/Shared/`, since none of this is generic yet).
- Check [TODO.md](TODO.md) before starting anything - it lists known gaps (no refresh-token flow, single-user token-store simplification, no order placement, etc.) so you don't rediscover them the hard way.
