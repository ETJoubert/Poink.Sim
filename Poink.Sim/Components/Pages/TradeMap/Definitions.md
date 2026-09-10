Working glossary for the TradeMap diagram's vocabulary. Draft -- captures what we've settled on so far, to be promoted/formalized later.

## Node kinds (`TradeMapPartKind`)

- **Page** -- the routable part. Owns layout and cross-panel state, nothing else. No business logic.
- **Panel** -- a UI-rendering unit. Can display and/or accept control input (a Blazor component in the framework sense -- see note below on why we didn't call this "Component").
- **Driver** -- a narrow, single-consumer adapter to one external capability. No knowledge of anything outside its own scope, no cross-cutting decisions. Example: `ICTraderSessionService`, `ISymbolService`, `ISymbolMetadataCache`.
- **Coordinator** -- sits above multiple panels/drivers and owns whatever requires knowing about more than one of them: shared state, and decisions that only make sense with that broader view. Example (proposed): `TradeCoordinator` owning selected-symbol propagation and settings persistence.
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

## Terminology notes

- **"Component" is overloaded across domains.** In general engineering (electronics, systems diagrams) it means any discrete block with defined inputs/outputs -- a resistor, a sensor, a subsystem. Frontend frameworks (React, then Blazor) reused the word for a much narrower, specific pattern: a self-contained unit of *UI rendering* with its own lifecycle, template, and a props-in/events-out contract. Since almost everything in this diagram has I/O, "has I/O" isn't a distinguishing trait -- so we avoid "Component" as a kind label and use **Panel** for the UI-rendering role instead, to stop the two senses colliding.
- **"Controller" is also overloaded** (MVC controller, embedded control loop, game controller, PID controller) and doesn't cleanly name what we mean by the cross-panel orchestrator role. We use **Coordinator** instead.
- **"Service"** is what .NET calls anything injectable, which is too broad to be useful as a kind label once a diagram has several different *kinds* of injectable thing. We use **Driver** for the narrow, single-consumer adapters (matches the embedded-systems sense: a driver talks to one thing and knows nothing else exists).
- **PROPOSED nodes** (currently `TradePage` as a pure container and `TradeCoordinator`) describe a target architecture, not the current implementation. They're marked as such in their own Detail text and in the Coordinator kind's legend tooltip so the diagram can't be mistaken for documentation of the real code.
