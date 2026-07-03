# Poink.CTrader Logbook

Chronological record of work done on the cTrader Open API integration. See [TODO.md](TODO.md) for outstanding work.

## 2026-07-03

- Investigated cTrader Open API + protobuf transport (TCP/SSL or WebSocket, `ProtoMessage` envelope, app auth -> account auth flow).
- Compared options: reference Spotware's `OpenAPI.Net` NuGet package (net6.0) vs vendor the source into our own net10.0 project. Chose to vendor, mainly for trim/AOT-friendliness on the MAUI targets in `Poink.csproj` (net10.0-android/ios/maccatalyst/windows).
- Cloned `https://github.com/ETJoubert/OpenAPI.Net.git` (user's fork of `spotware/OpenAPI.Net`) to inspect source.
  - Finding: the repo does **not** ship raw `.proto` files — only the already protoc-generated C# classes (`Messages/OpenApiMessages.cs`, `OpenApiModelMessages.cs`, `OpenApiCommonMessages.cs`, `OpenApiCommonModelMessages.cs`). So there is no proto compilation step to perform ourselves; vendoring is a copy + retarget, not a rebuild-from-proto.
  - `OpenClient.cs` uses only modern, portable APIs (`TcpClient`, `SslStream`, `System.Threading.Channels`, `System.Buffers`, Rx) - nothing net6.0-specific.
- Created `Poink.CTrader` class library (net10.0), added to `Poink.sln`.
- Copied `Auth/`, `Exceptions/`, `Helpers/`, `Messages/`, `OpenClient.cs` from the fork into `Poink.CTrader`.
- Retargeted `Poink.CTrader.csproj` to `net10.0` and bumped package references to current stable versions:
  - `Google.Protobuf` 3.20.1 -> 3.35.1
  - `System.Reactive` 5.0.0 -> 6.1.0
  - `Websocket.Client` 4.4.43 -> 5.5.0
- Verified `dotnet build` succeeds: 0 errors, 24 nullable-reference warnings (pre-existing code wasn't written with `Nullable enable`; see TODO).

## 2026-07-03 (continued) - secrets/token storage

- Surveyed existing config conventions: static secrets follow an options-class + config-section pattern (`JwtOptions`/`JwtOptionsInstaller`, auto-discovered by `Poink.Shared.Core.ServiceRegistrar` via reflection over `IConfiguredServiceInstaller` implementations *in the `Poink.Shared` assembly only*). Per-user state already lives in Postgres via `PoinkContext` (EF Core, `Poink.Shared/Migrations`). No Key Vault or third-party-credential precedent exists (MailKit credentials are passed per-request, not persisted).
- Decision: keep `Poink.CTrader` free of any EF/Postgres dependency so it stays usable standalone in other projects.
  - `Poink.CTrader` now defines `CTraderApiOptions` (static app ClientId/ClientSecret/RedirectUri/environment flag, bound via `IOptions<T>`), `ICTraderTokenStore` (an abstraction over per-user OAuth token persistence, keyed by `Guid userId`, using the vendored `OpenAPI.Net.Auth.Token` DTO), and an `AddCTraderOpenApi(IConfiguration)` DI extension that only binds options - consuming projects must register their own `ICTraderTokenStore`.
  - `Poink.Shared` (which already owns `PoinkContext`/`User`) implements the concrete storage: new `CTraderAccountToken` entity + `DbSet`, `PostgresCTraderTokenStore : ICTraderTokenStore`, and `CTraderOptionsInstaller : IConfiguredServiceInstaller` that wires both up (auto-discovered by `ServiceRegistrar` since it lives in the `Poink.Shared` assembly). Added `ProjectReference` from `Poink.Shared` to `Poink.CTrader`.
  - EF migration `20260703104751_AddCTraderAccountToken` created (`dotnet ef migrations add AddCTraderAccountToken --project Poink.Shared --context PostgresPoinkContext`). **Not yet applied to any database** - run `dotnet ef database update --project Poink.Shared --context PostgresPoinkContext` when ready.
- Static App ID/Secret placement: added an empty `"CTrader": { "ClientId": "", "ClientSecret": "", "RedirectUri": "", "UseLiveEnvironment": false }` section to `Poink/appsettings.json` and `Poink.Web/appsettings.json` (placeholders only - real values must never be committed, unlike the existing `JwtSettings` dev secret which the repo already accepts committing as a local-only dev value).
  - `Poink.Web` (ASP.NET Core): added `<UserSecretsId>` so `dotnet user-secrets set CTrader:ClientSecret ...` works via the framework's built-in Development-time secrets provider.
  - `Poink` (MAUI): no built-in user-secrets pipeline exists (config is loaded from embedded `appsettings*.json` streams in `MauiProgram.cs`). Added `builder.Configuration.AddEnvironmentVariables()` plus the `Microsoft.Extensions.Configuration.EnvironmentVariables` package reference, so real values can be supplied as `CTrader__ClientId` / `CTrader__ClientSecret` env vars (double-underscore = nested key) without touching a tracked file.
- Verified builds: `Poink.CTrader`, `Poink.Shared`, and `Poink` (net10.0-windows target) all build with 0 errors. `Poink.Web` was not rebuilt in this session because a running `Poink.Web.exe` instance held its output files locked; the only changes there were an `appsettings.json` placeholder and a `<UserSecretsId>` addition, neither of which affects compilation.

## 2026-07-03 (continued) - OAuth login page + end-to-end verification

- Confirmed via Spotware's own WPF sample (`AccountAuthViewModel.cs` in the cloned fork) that the OAuth redirect URI never needs to be reachable: a native browser control navigates to `App.GetAuthUri()`, and the app intercepts the *attempted* navigation to the redirect URI client-side (cancelling it before it's ever requested), reading `code`/`error` off that URL. This meant the existing registered redirect URI (an old implementation's HTTPS endpoint) did not need to change.
- Surveyed the MAUI app's architecture (`Poink/MainPage.xaml` is a single `ContentPage` hosting one `BlazorWebView` - no Shell/NavigationPage): confirmed `Application.Current.MainPage.Navigation.PushModalAsync(...)` works out of the box for presenting a native page over the Blazor content, since `ContentPage` always has an implicit `Navigation` proxy. Also confirmed the NavMenu is driven by `BuiltInFeatures.All` (an `IFeature` list), not per-entry markup, and that this would be the first "Blazor calls native MAUI" interop in the codebase (no existing precedent).
- Implemented, following the platform-registration pattern already used for `ITokenStore`/`LocalStorageTokenStore` (registered directly in `MauiProgram.cs`, not through `Poink.Shared`'s reflection-based `ServiceRegistrar`):
  - `Poink.CTrader`: added `ICTraderAuthUiService` (platform-agnostic interface: capture an authorization code given the auth URI + redirect URI) and registered an `OpenAPI.Net.Auth.App` DI factory (built from `CTraderApiOptions`) in `AddCTraderOpenApi`.
  - `Poink` (native): `Pages/CTraderLoginPage.xaml(.cs)` - a `ContentPage` with a `WebView`, whose `Navigating` handler intercepts the redirect, extracts `code`/`error` via a small hand-rolled query parser (avoided adding a `System.Web`/`HttpUtility` dependency, which isn't guaranteed available on all MAUI target frameworks), and resolves a `TaskCompletionSource<string>`. `Services/MauiCTraderAuthUiService.cs` pushes/pops this page modally. Registered in `MauiProgram.cs` alongside the other MAUI-specific service registrations.
  - `Poink.Shared`: added a `CTrader` entry to `BuiltInFeatures` (nav route `/ctrader`) and a new Blazor page `Components/Pages/CTrader/CTraderPage.razor(.cs)` that: calls `ICTraderAuthUiService` to get a code, exchanges it via `TokenFactory.GetToken`, saves the token via `ICTraderTokenStore` (keyed to the first `User` row found - see TODO, this doesn't yet resolve the *actual* logged-in user), then opens an `OpenClient`, sends `ProtoOAApplicationAuthReq` + `ProtoOAGetAccountListByAccessTokenReq`, and displays the returned trading accounts (or the error) as the end-to-end verification step.
  - Enabled the `"CTrader"` feature key in `Poink/appsettings.json`'s `Features:Enabled` list so the nav entry actually shows up.
- Verified builds: `Poink.Shared` and `Poink` (net10.0-windows target) both build with 0 errors after all of the above.

## 2026-07-03 (continued) - first live end-to-end run

- First test run hit `Failed: No authorization code was returned. Redirected URL: https://openapi.ctrader.com/apps/auth?client_id=&redirect_uri=&scope=trading` - root cause was empty `CTraderApiOptions` (env vars set via `setx` don't apply to an already-running Visual Studio process; needed a full VS restart). This also exposed a real bug: an empty redirect URI made `CTraderLoginPage`'s `StartsWith` check match *any* URL, cancelling navigation to cTrader's own login page before the user ever saw it. Fixed both: added an explicit "cTrader is not configured" check in `CTraderPage.ConnectAsync` before starting the flow, and a guard in `CTraderLoginPage.OnNavigating` so an empty redirect URI can never match.
- After fixing the env var setup, login/consent succeeded but `VerifyConnectionAsync` failed with `UNSUPPORTED_MESSAGE: Trading account is not authorized`. Root cause: `ProtoOAApplicationAuthReq` and `ProtoOAGetAccountListByAccessTokenReq` were sent back-to-back via `SendMessage` (which only enqueues) without waiting for `ProtoOAApplicationAuthRes` - the account-list request raced ahead of app authorization and was rejected. Fixed by awaiting the app-auth response before sending the account-list request.
- Wired `LogService` (the app's existing "Console Log" panel, `Poink.Shared/Services/LogService.cs`) through the whole connect flow - every step and every raw message received now logs there for visibility.
- **Confirmed working end-to-end against the user's real cTrader account: real trading accounts were returned.** This is the first fully verified proof that config -> OAuth -> token storage -> live `OpenClient` connection -> real API data all work together.

## 2026-07-03 (continued) - extracted CTraderClient

- With the connection proven working, extracted the ad hoc `OpenClient` handling out of `CTraderPage.razor.cs` into a reusable `Poink.CTrader.ICTraderClient`/`CTraderClient`:
  - `ConnectAsync` opens the socket and completes the app-auth handshake as one step.
  - `GetAccountsAsync` / `AuthorizeAccountAsync` are typed request/response helpers built on a generic `SendAndWaitAsync<TRes>` (subscribes for the expected response type or a `ProtoOAErrorRes` -> throws `CTraderApiException`, with a timeout).
  - Exposes a public `Messages` (`IObservable<IMessage>`, backed by an `System.Reactive.Subjects.Subject` so it's available even before `ConnectAsync` is called) so callers can still observe every raw message - `CTraderPage` subscribes to this purely to forward log lines to `LogService`, keeping `Poink.CTrader` itself decoupled from `Poink.Shared`'s logging.
  - Registered in `AddCTraderOpenApi` via DI, resolving the already-registered `App` + `CTraderApiOptions.UseLiveEnvironment`.
  - One instance is good for exactly one connection attempt (mirrors the vendored `OpenClient`'s own single-use design) - `CTraderPage` does `await using var client = new CTraderClient(...)` per verification click, same lifecycle as before.
- `CTraderPage.VerifyConnectionAsync` shrank from ~70 lines of manual subscription/`TaskCompletionSource` plumbing to ~15 lines using the new client, with identical behavior and logging.
- Verified `Poink.CTrader` and `Poink.Shared` both build with 0 errors after the refactor.

## 2026-07-03 (continued) - account selection + symbols page

- Added `GetSymbolsAsync(ctidTraderAccountId, includeArchivedSymbols)` to `ICTraderClient`/`CTraderClient` (`ProtoOASymbolsListReq` -> `ProtoOASymbolsListRes`), same `SendAndWaitAsync` pattern as the existing methods.
- Reworked `CTraderPage` so the `CTraderClient` connection now survives across button clicks (previously it was `await using`-scoped to a single verification call and disposed immediately): the page holds `_client`/`_token`/`_logSubscription` as fields, disposes/reconnects them at the start of a fresh "Connect" click, and implements `IAsyncDisposable` so Blazor disposes the connection when navigating away from the page. This was necessary because authorizing an account and then requesting its symbols both have to happen on the *same* authenticated connection.
- Page flow is now: Connect -> list accounts -> user picks one ("Select" button) -> `AuthorizeAccountAsync` for that account -> `GetSymbolsAsync` -> display.
- For the page layout, used the app's existing generic `Poink.Shared.Components.Shared.DataTable.DataTable<TItem>` component instead of hand-rolled HTML, bound to two small plain DTOs (`CTraderAccountRow`, `CTraderSymbolRow`) rather than the raw protobuf message types - the real `ProtoOACtidTraderAccount`/`ProtoOALightSymbol` classes have many more fields than are useful to show and would have produced noisy auto-generated columns.
- Fixed an unrelated pre-existing warning while in the area: `MauiCTraderAuthUiService` used the obsolete `Application.Current.MainPage`; switched to `Application.Current.Windows.FirstOrDefault()?.Page`.
- Verified `Poink.CTrader`, `Poink.Shared`, and `Poink` (net10.0-windows target) all build with 0 errors and 0 warnings.

## 2026-07-03 (continued) - historical trendbars + searchable symbol side panel

- Confirmed cTrader's Open API supports historical OHLC data via `ProtoOAGetTrendbarsReq`/`Res` - takes a `Period` (`ProtoOATrendbarPeriod`: M1...M30, H1/H4/H12, D1, W1, MN1) and a `FromTimestamp`/`ToTimestamp` (unix ms) range. Added `GetTrendbarsAsync(ctidTraderAccountId, symbolId, period, from, to)` to `ICTraderClient`/`CTraderClient`.
- `ProtoOATrendbar` bars are delta-encoded to save bytes: `Low` is the absolute low (scaled 1/100_000, same as spot prices), and `DeltaOpen`/`DeltaHigh`/`DeltaClose` are offsets *added* to `Low` to get the actual open/high/close. `UtcTimestampInMinutes` is minutes-since-epoch, not milliseconds - multiply by 60 before converting to a `DateTimeOffset`. All of this came straight from the generated code's own doc comments, not guessed.
- **Known caveat, not yet handled specially:** cTrader enforces its own maximum time range per period server-side (e.g. minute bars can't span as far back as daily bars can) - an invalid combination surfaces as a normal `CTraderApiException` from the existing error handling, not a special case. If the user picks e.g. "Last month" + "Minute", they may see a real cTrader rejection rather than a client-side validation message.
- Redesigned `CTraderPage`'s layout per user request - the symbol list no longer takes up the whole page:
  - Moved to an `<aside class="sidebar">` side panel (reusing the same `page-with-sidebar`/`sidebar` CSS classes `Chat.razor` already uses), with a plain text filter input (`FilteredSymbols` does a simple case-insensitive `Contains` over the in-memory list - no need for the heavier `DataTable` component here, which doesn't fit a narrow sidebar well).
  - Selecting a symbol in the sidebar now does double duty: subscribes to its live spot price (as before, renamed `WatchSymbolAsync` -> `SelectSymbolAsync`) *and* becomes the target for the new history controls in the main content area.
  - Main content area now shows: selected symbol's live bid/ask, a Range dropdown (Last Hour/Day/Week/Month) and Timeframe dropdown (Minute/Hour/Day), a "LOAD HISTORY" button, and the resulting bars in a `DataTable<CTraderBarRow>`.
- Verified `Poink.CTrader`, `Poink.Shared`, and `Poink` (net10.0-windows target) all build with 0 errors and 0 warnings.

- Added `SubscribeSpotsAsync`/`UnsubscribeSpotsAsync` to `ICTraderClient`/`CTraderClient` (`ProtoOASubscribeSpotsReq`/`ProtoOAUnsubscribeSpotsReq`, both take a `CtidTraderAccountId` + repeated `SymbolId`). These only confirm the subscription was accepted - the actual live ticks arrive as `ProtoOASpotEvent` push messages on the already-public `Messages` observable, no new per-tick API needed.
- Found (from the generated code's own doc comment) that `ProtoOASpotEvent.Bid`/`.Ask` are integers scaled by 1/100_000 of a unit (e.g. `123000` -> `1.23`) - added a `PriceScale` constant in `CTraderPage` for the conversion. Used `HasBid`/`HasAsk` (protobuf's optional-field presence flags) rather than checking for zero, since a spot event may only carry one side of the quote.
- `CTraderPage` now has a "WATCH PRICE" action per symbol row: subscribes to that symbol's spots, unsubscribing from whatever was previously watched first, then filters `client.Messages` for `ProtoOASpotEvent` matching that symbol id. Since spot events arrive on a background thread (not Blazor's render sync context), the update goes through `InvokeAsync(() => { ...; StateHasChanged(); })` rather than calling `StateHasChanged()` directly.
- Selecting a different account, or reconnecting entirely, now also tears down the price watch/subscription, since a spot subscription is scoped to one account+symbol pair.
- Verified `Poink.CTrader`, `Poink.Shared`, and `Poink` (net10.0-windows target) all build with 0 errors and 0 warnings.
