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
- [ ] Add a Poink-specific service wrapping `OpenClient` that:
  - sequences app auth -> account auth
  - picks demo vs live host/port
  - handles reconnect/backoff (the vendored `OpenClient` does not reconnect automatically)
  - exposes a friendlier API surface to consuming projects
- [ ] Add a `ProjectReference` to `Poink.CTrader` from whichever project needs it first (likely `Poink.Shared` or `Poink`) once the wrapper API is ready.
- [ ] Decide TCP vs WebSocket transport for our use case.
- [ ] Clean up the 24 nullable-reference warnings in vendored code (either annotate properly or scope-disable `Nullable` for this vendored code - decide which fits our conventions).
- [ ] Add a test project (`Poink.CTrader.Tests`) - decide unit vs. integration (real demo account) coverage.
- [ ] Test end-to-end against a demo account before ever pointing at live.
- [ ] Add a short README to `Poink.CTrader` documenting how to reference and use it from other projects in the solution.
- [ ] Decide how to handle upstream updates: keep tracking `spotware/OpenAPI.Net` for new message types/fixes, or treat this as a one-time fork with no further syncing.
