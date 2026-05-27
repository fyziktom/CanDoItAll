# MAF 1.6 Official Notes Applied

## Package baseline

- `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` references `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, and `Microsoft.Agents.AI.Workflows` at `1.6.2`.
- `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` references `Microsoft.Agents.AI.A2A` at `1.6.2-preview.260521.1`.
- `repo://src/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj` references `Microsoft.Agents.AI.Hosting.A2A` at `1.6.2-preview.260521.1`.
- Static package audit found no stale `1.3` MAF package references under `src` or `tests`.

## Local API reality check

- The referenced `Microsoft.Agents.AI` package exposes `MessageAIContextProvider` and context-provider attachment. The local package search did not expose `IChatMessageInjector`, so CanDoItAll keeps finalizer instructions explicit and uses context providers for runtime context injection.
- `AgentSessionFiles` was not present in the referenced MAF package set. The current durable artifact path remains CanDoItAll managed storage plus bounded MAF session serialization.
- A2A and workflow-as-agent APIs are present and exercised through the current adapter source and tests.
- OpenTelemetry is wired through the MAF builder and CanDoItAll telemetry boundary; the adapter keeps process-level tags in CanDoItAll source.

## Execution decision

This bundle does not force unsupported symbols into production code. It records each MAF 1.6 feature as adopted, deferred, or guarded, and validates the resulting adapter and process-runtime behavior with source audits, test buckets, browser smoke, and a deterministic agent handoff smoke.
