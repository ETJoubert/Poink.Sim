# Poink.CTrader TODO

Outstanding work for the cTrader Open API integration. See [LOGBOOK.md](LOGBOOK.md) for what's already done.

## Next up

- [x] ~~Decide where App ID/Secret and OAuth access/refresh tokens are stored~~ - static App ID/Secret via `CTraderApiOptions` (options pattern, config section `CTrader`), per-user tokens via `ICTraderTokenStore` backed by Postgres (`CTraderAccountToken` entity in `Poink.Shared`). See LOGBOOK.
- [ ] Set real `ClientId`/`ClientSecret`/`RedirectUri` values locally: for the MAUI app, use `CTrader__ClientId` / `CTrader__ClientSecret` / `CTrader__RedirectUri` environment variables (the redirect URI embeds a real IP, so it should stay out of tracked files too, not just the secret). For `Poink.Web`, use `dotnet user-secrets set CTrader:ClientSecret "..." --project Poink.Web`. Never fill any of these into a tracked `appsettings*.json`.
- [x] ~~Apply the new EF migration to your actual database~~ - not needed manually: `MauiProgram.cs` already calls `context.Database.MigrateAsync()` on startup, so it applies automatically as long as Postgres is reachable at the configured connection string.
- [x] ~~Verify `Auth/TokenFactory.cs` OAuth flow works outside a traditional ASP.NET redirect-callback context~~ - built a native `WebView`-based login page (`Poink/Pages/CTraderLoginPage.xaml`) that intercepts the redirect client-side; see LOGBOOK. The redirect URI never needs to be reachable.
- [ ] The `CTraderPage` currently saves the token against the *first* `User` row in the database, not the actually-logged-in user (no `ICurrentUserService`/claims-based lookup exists yet in this codebase). Fine for a single-user app; revisit if multi-user support is ever added.
- [ ] `CTraderLoginPage`'s query-string parser is hand-rolled (deliberately, to avoid a `System.Web.HttpUtility` dependency that isn't guaranteed available across all MAUI target frameworks) - fine for simple `code`/`error` params, but doesn't handle multi-value keys or `+`-encoded spaces.
- [ ] No refresh-token flow wired up yet (`ProtoOARefreshTokenReq`/`Res` per the vendored `OpenClient`) - access tokens will eventually expire and need silent refresh instead of re-running the whole login page.
- [x] ~~Fix race condition sending `ProtoOAGetAccountListByAccessTokenReq` before `ProtoOAApplicationAuthRes` came back~~ - `VerifyConnectionAsync` now awaits app-auth before requesting the account list. Wired `LogService` (the app's existing Console Log panel) through the whole connect flow for visibility.
- [x] ~~Add a Poink-specific service wrapping `OpenClient`~~ - `ICTraderClient`/`CTraderClient` in `Poink.CTrader`: `ConnectAsync` (opens the socket + completes the app-auth handshake), `GetAccountsAsync`, `AuthorizeAccountAsync`, a generic `SendAndWaitAsync<TRes>` helper correlating requests to responses (or a `ProtoOAErrorRes`/timeout), and a public `Messages` observable so callers can still watch every raw message (used by `CTraderPage` for its console logging). Registered in DI via `AddCTraderOpenApi`. `CTraderPage.VerifyConnectionAsync` shrank from ~70 lines of ad hoc subscription/TCS code to ~15 lines using this client.
  - Reconnect/backoff is still **not** handled - one `CTraderClient` instance is good for exactly one connection attempt (construct a new one to retry). Revisit if/when this needs to run unattended for longer than one interactive check.
- [x] ~~Add a `ProjectReference` to `Poink.CTrader` from whichever project needs it first~~ - `Poink.Shared` already references it (see earlier LOGBOOK entries).
- [x] ~~`AuthorizeAccountAsync` isn't called from anywhere yet~~ - `CTraderPage` now calls it when the user selects an account, then loads that account's symbols via the new `GetSymbolsAsync`.
- [x] ~~Symbols are fetched but not yet used for anything beyond display~~ - added a "WATCH PRICE" action per symbol that subscribes to live spot quotes (`ProtoOASubscribeSpotsReq`/`ProtoOASpotEvent`) and shows current bid/ask on the page.
- [ ] Only one symbol can be watched at a time (watching a new one unsubscribes the previous). Fine for now; would need a small dictionary of subscriptions to watch several at once.
- [ ] No trendbars (historical candles) or order placement yet.
- [ ] `CTraderPage` currently only authorizes one account at a time (selecting a different account doesn't de-authorize the previous one server-side, it just changes what the page displays) - fine for now, revisit if multi-account-at-once views are ever needed.
- [ ] Decide TCP vs WebSocket transport for our use case.
- [ ] Clean up the 24 nullable-reference warnings in vendored code (either annotate properly or scope-disable `Nullable` for this vendored code - decide which fits our conventions).
- [ ] Add a test project (`Poink.CTrader.Tests`) - decide unit vs. integration (real demo account) coverage.
- [ ] Test end-to-end against a demo account before ever pointing at live.
- [ ] Add a short README to `Poink.CTrader` documenting how to reference and use it from other projects in the solution.
- [ ] Decide how to handle upstream updates: keep tracking `spotware/OpenAPI.Net` for new message types/fixes, or treat this as a one-time fork with no further syncing.
