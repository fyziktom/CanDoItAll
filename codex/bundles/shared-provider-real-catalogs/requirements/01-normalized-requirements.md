# Normalized Requirements

- R1 / N001: Kind change clears incompatible catalog, pricing and connection state.
  Successful Ollama discovery exactly replaces model membership with /api/tags names.
- R2 / N002: Discovery replaces stale model membership and removes stale price rows.
  Missing prices stay unknown; no custom-model/e2e names or invented rates are generated.
  OpenAI published prices are verified against official documentation; model inventory
  comes from the configured upstream, never from a price list.
- R3 / N003: Client mirrors the full source catalog, real display names, prices and private
  flag; no additional built-in rows. Routing IDs remain internal. Nondefault selection works.
- R4 / N004: New durable bundle, targeted regression proof and two rebuilt Docker apps.
  UI configuration and real chat/agent/image/vision execution plus source usage proof.
  Final handed-off apps must not use fixture URLs or synthetic catalogs.
- R5 / N005: normal local browser users can create, save and execute Simple Chats on
  the Docker client. Headless OS does not imply non-interactive browser. Explicit
  local Docker ingress trust is isolated from remote/API and dev-route authorization.
