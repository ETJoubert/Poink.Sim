# Poink.CTrader TODO

Outstanding work for the cTrader Open API integration. See [README.md](README.md) for an architecture overview.

## Open

- [ ] No refresh-token flow wired up yet (`ProtoOARefreshTokenReq`/`Res` per the vendored `OpenClient`) - an expired access token means logging in again.
- [ ] Reconnect/backoff is not handled - one `CTraderClient` instance is good for exactly one connection attempt.
- [ ] Tokens are stored for a single user; revisit if multi-user support is ever added.
- [ ] Wide range + fine timeframe trendbar requests may exceed cTrader's server-side max-range-per-period limit - currently surfaces as whatever error cTrader returns, with no client-side pre-validation.
- [ ] Decide TCP vs WebSocket transport.
- [ ] Clean up the nullable-reference warnings in vendored code (annotate properly or scope-disable `Nullable` for the vendored folders).
- [ ] Add unit coverage for `CTraderClient` (request/response correlation, error and timeout paths).
- [ ] Decide how to handle upstream updates: keep tracking `spotware/OpenAPI.Net` for new message types/fixes, or treat this as a one-time fork.
