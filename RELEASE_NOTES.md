# Release Notes

## 1.0.0

This release is aimed at engineers building host-side bridge products, not end users.

Typical adopters include:

- game mods exposing a GABP endpoint from inside a running game
- embedded automation bridges supervising a game or application process
- product-specific bridge hosts in the style of RimBridgeServer

### Highlights

- Stable release line aligned with `Gabp.Runtime 1.0.0`
- Ships both `netstandard2.0` and `net10.0` assets
- Adds optional attention APIs: `attention/current`, `attention/ack`, `attention/opened`, `attention/updated`, and `attention/cleared`
- Improves `tools/list` metadata with canonical `title`, `inputSchema`, `outputSchema`, `ResultDescription`, and structured `[ToolResponse]` fields
- Fixes parameter metadata and binding so optional/defaulted parameters are advertised and applied correctly

### Compatibility

- Intended to work in modern .NET hosts and in Unity/Mono-style hosts that can consume `.NET Standard 2.0`
- Validated locally against a RimBridgeServer-style consumer build targeting `net472` and deploying into `RimWorldMac.app`
- Depends on `Gabp.Runtime 1.0.0`

### Audience Guidance

Lib.GAB is the host/runtime layer. It helps you expose a stable GABP surface from inside your product, but it does not replace product-specific logic.

You still own:

- capability naming and semantics
- access to game or application state
- event production policy
- attention policy
- packaging and deployment inside your host product

### Upgrade Notes

- Existing hosts that run inside Unity, Mono, or .NET Framework do not need a .NET 10 runtime. They consume the `netstandard2.0` asset.
- Existing hosts should revisit any custom workarounds around optional parameters or default values in tool metadata. `tools/list` and tool invocation now agree on defaulted parameters.
- Product teams that want richer downstream discovery should start filling in `ResultDescription` and `[ToolResponse]` where their tools return stable, meaningful result shapes.

### Recommended Adoption Pattern

1. Reference `Lib.GAB` from the host-side component that actually runs inside the game or application.
2. Register product-specific tools explicitly or by attribute scanning.
3. Surface `ResultDescription` and `[ToolResponse]` metadata where downstream bridges benefit from richer discovery.
4. Enable attention support only when your product has a clear policy for opening, updating, and acknowledging blocking async state.
