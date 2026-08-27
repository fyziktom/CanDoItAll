# Initial State And Recovery

Re-anchored 2026-08-27 after host restart, HEAD 0ecb6307823576e80f79074187668771b166609a.
Existing dirty work is preserved. This bundle had no production edits before restart.

- Source 5210 UI Shared Ollama had Kind Ollama but a fixture URL, default e2e-ollama,
  OpenAI tags, and 27 mixed/synthetic price rows. Its client mirrored polluted data.
- Kind binding retains incompatible state. Discovery merges stale rows. Normalization
  repopulates empty prices and manufactures a default-model price. The catalog policy
  injects built-in OpenAI suggestions. Add price invents custom-model names. Refresh
  changes prices without replacing suggested model membership.
- Real OpenAI /v1/models succeeded with the authorized key. Real IDs include
  gpt-5.6-sol/terra/luna, not the plain gpt-5.6 seed.
- Real Ollama 192.168.10.132:11434, confirmed in 5032 UI, refuses connections even
  after restart. User was asked whether its address changed.
- Source/client/database containers survived and were restarted with existing volumes.
- CodeAnalytics snapshot snap-20260827130822-7f10b6cc scoped ProviderManagement:
  70 documents, no blocking diagnostics. Project loader omits reference graph, so
  this is not whole-solution dependency proof.

Owners: Models price rules, ProviderManagement discovery/persistence, SharedProviders
publication, and Blazor editor transitions. No project-reference changes are needed.

## Subsequent recovery

The Ollama outage above is historical: it recovered on 2026-08-27 and returned 72
installed models. UI refresh replaced the polluted source profile; full source/client
parity passed alongside 128 real OpenAI and five real image-model identities.
Live validation exposed three additional boundaries: real image-tool names needed
constrained route resolution, the image relay rejected documented OpenAI response
metadata, and the OpenAI compatibility client used routing IDs instead of source model
names to select existing tool-call rules. Failing-first regressions and rebuilds are
recorded in SB01; actual end-to-end closure is tracked in the execution report.
