Working glossary for the TradeMap diagram's vocabulary. Draft -- captures what we've settled on so far, to be promoted/formalized later.

## Node kinds (`TradeMapPartKind`)

- **Page** -- the routable part. Owns layout and cross-panel state, nothing else. No business logic.
- **Panel** -- a UI-rendering unit. Can display and/or accept control input (a Blazor component in the framework sense -- see note below on why we didn't call this "Component").
- **Driver** -- the concrete implementation behind one external capability (e.g. `CTraderSessionService`). Consumers never depend on a Driver directly -- they depend on one of its segregated Interfaces instead.
- **Interface** -- a narrow, single-consumer(-role) contract exposed by one Driver. No knowledge of anything outside its own scope, no cross-cutting decisions -- matches the embedded-systems sense of "driver" (talks to one thing, doesn't know anything else exists), which is why the underlying implementation node is still called Driver and this is the thing that actually gets injected. Drawn with a dashed border (the same "abstract, not concrete" treatment as Page's wrapping frame) plus a UML lollipop dot on the title, so it reads as a contract, not a part -- square like every other card, so the shape doesn't fight the port dots along its edges the way a fully-rounded pill did. Example: `IWatchlistAccess`, `ITradeExecutionAccess`, `ICTraderHistory` (the one real example already in the code today).
- **Coordinator** -- owns only what has no Interface of its own: state that requires knowing about more than one panel at once. Anything a Driver already owns (positions, quotes, watchlist, orders) is reached by panels directly through an Interface, never through the Coordinator -- see "Coordinator vs. Interface" below. Example (proposed): `TradeCoordinator` owning selected-symbol propagation, sidebar-collapsed state, and chart-settings persistence.
- **Helper** -- a static, pure calculation function. No dependencies, no state.
- **ExternalApi** -- a boundary to something outside the process (e.g. the cTrader Open API).
- **DataStore** -- a boundary to persisted data (e.g. `PoinkSimContext` / Postgres).
- **UserSpace** -- the buttons and inputs a person interacts with directly. Not code; represents the human.

## Signal kinds (`TradeMapSignalKind`)

- **Control** -- an imperative call: one part telling another to *do* something (method invocation, one-shot request/response). Flows backward -- toward whatever originated the action.
- **Display** -- state exposed continuously: a property read every render, or a subscribed event. Flows forward -- toward whatever displays it.

## Port model

A port's side is decided by its signal kind, not just its direction:

- **Display** port: input on the card's left edge, output on the right. Stacks in the top band of the card (below the header).
- **Control** port: input on the right edge, output on the left -- flipped, because the signal is running backward. Stacks in the bottom band of the card (above the footer).

A connector whose ports already face each other (forward Display, backward Control) curves directly. One whose ports face away -- same column, or a signal running against its own kind's grain -- loops through a routed lane instead.

## Coordinator vs. Interface

These two solve different problems and don't compete:

- **Interface segregation controls who can command what** -- it's about outbound capability. Each panel injects only the narrow Interface(s) it actually needs, so `FinancialsPanel` can't accidentally reach `PlaceOrderAsync` just because it's sitting on some shared object.
- **Interfaces already propagate state on their own** -- a Driver is a singleton that raises its own change events (e.g. `ICTraderSessionService.Changed`), so when `TradeControlsPanel` places an order and the Driver's positions update, any panel holding the relevant Interface finds out directly. No coordinator has to relay that -- it's Observer/pub-sub, not Mediator.
- **The Coordinator is what's left over**: state with no Driver owner at all, because it's a pure UI/application notion, not a domain fact. "Which symbol is currently selected" isn't something `ICTraderSessionService` knows or should know -- nothing but the UI layer invented that concept, so it has nowhere else to live.

**Extending a panel's capability later** is additive, not a wall to tear down: if `SymbolWatchlistPanel` later needs to place trades, it just also injects `ITradeExecutionAccess` alongside `IWatchlistAccess` -- a one-line, compiler-checked change. Nothing about `TradeControlsPanel`, the Coordinator, or the underlying Driver has to move.

## Terminology notes

- **"Component" is overloaded across domains.** In general engineering (electronics, systems diagrams) it means any discrete block with defined inputs/outputs -- a resistor, a sensor, a subsystem. Frontend frameworks (React, then Blazor) reused the word for a much narrower, specific pattern: a self-contained unit of *UI rendering* with its own lifecycle, template, and a props-in/events-out contract. Since almost everything in this diagram has I/O, "has I/O" isn't a distinguishing trait -- so we avoid "Component" as a kind label and use **Panel** for the UI-rendering role instead, to stop the two senses colliding.
- **"Controller" is also overloaded** (MVC controller, embedded control loop, game controller, PID controller) and doesn't cleanly name what we mean by the cross-panel orchestrator role. We use **Coordinator** instead.
- **"Service"** is what .NET calls anything injectable, which is too broad to be useful as a kind label once a diagram has several different *kinds* of injectable thing. We use **Driver** for the underlying implementation and **Interface** for what's actually injected (matches the embedded-systems sense: a driver talks to one thing and knows nothing else exists).
- **PROPOSED nodes** (currently `TradePage`, `TradeCoordinator`, and every Interface pill except `ICTraderHistory`) describe a target architecture, not the current implementation -- today every panel injects the whole Driver directly. They're marked as such in their own Detail text and in the relevant kind's legend tooltip so the diagram can't be mistaken for documentation of the real code.
