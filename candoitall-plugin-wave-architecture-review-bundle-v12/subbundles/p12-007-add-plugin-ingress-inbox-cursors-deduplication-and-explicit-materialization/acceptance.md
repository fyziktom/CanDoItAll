# Acceptance

- Add a durable ingress envelope/inbox model.
- Add cursor persistence for polling-style plugins.
- Add dedupe based on source + external id / cursor semantics.
- Add explicit materialization handlers that turn accepted ingress envelopes into domain artifacts only when appropriate.
- Recommended exact types:
  - `PluginIngressEnvelopeRecord`
  - `PluginIngressCursorRecord`
  - `IPluginIngressInbox`
  - `IPluginIngressMaterializer`
